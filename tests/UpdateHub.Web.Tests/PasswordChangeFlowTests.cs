using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using Xunit;

namespace UpdateHub.Web.Tests;

/// <summary>
/// End-to-end coverage for the forced-password-change flow. Catches the two
/// concrete regressions we hit in manual testing:
///   1. POST to /account/password/change being eaten by
///      ForceChangePasswordMiddleware (the user clicked Save and nothing
///      visible happened).
///   2. /account/change-password landing on a route that 500-ed because the
///      Razor page and a Minimal API both bound the same path.
/// </summary>
public class PasswordChangeFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PasswordChangeFlowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task ChangePasswordPage_ServesGet_WhenAuthenticated()
    {
        // Just verifies the Razor component route is reachable. If MapPost
        // accidentally collides with the page route again, this would 500.
        var user = await SeedUserAsync("alice-page", "TempPassw0rd!", mustChange: true);
        await SignInAsync(user, "TempPassw0rd!");

        var res = await _client.GetAsync("/account/change-password");

        Assert.True(res.StatusCode == HttpStatusCode.OK ||
                    res.StatusCode == HttpStatusCode.Redirect, // antiforgery may redirect
            $"GET /account/change-password should not 500 — got {(int)res.StatusCode}");
    }

    [Fact]
    public async Task ChangePassword_Post_UpdatesHashAndClearsMustChangePassword()
    {
        var user = await SeedUserAsync("alice-flow", "TempPassw0rd!", mustChange: true);
        await SignInAsync(user, "TempPassw0rd!");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("current", "TempPassw0rd!"),
            new KeyValuePair<string, string>("new",     "NewPassw0rd!"),
            new KeyValuePair<string, string>("confirm", "NewPassw0rd!"),
        });

        var res = await _client.PostAsync("/account/password/change", form);

        // Successful POST redirects to / (dashboard)
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/", res.Headers.Location?.OriginalString);

        // The middleware must not have bounced us back to the GET page —
        // that was the bug where "nothing happened" on click.
        Assert.NotEqual("/account/change-password",
            res.Headers.Location?.OriginalString);

        // DB state: new bcrypt verifies, old one doesn't, MustChange cleared
        var refreshed = await GetUserAsync(user.Username);
        Assert.NotNull(refreshed);
        Assert.False(refreshed!.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassw0rd!", refreshed.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("TempPassw0rd!", refreshed.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_Post_RejectsWrongCurrentPassword()
    {
        var user = await SeedUserAsync("alice-wrong", "TempPassw0rd!", mustChange: true);
        await SignInAsync(user, "TempPassw0rd!");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("current", "WrongPassw0rd!"),
            new KeyValuePair<string, string>("new",     "NewPassw0rd!"),
            new KeyValuePair<string, string>("confirm", "NewPassw0rd!"),
        });

        var res = await _client.PostAsync("/account/password/change", form);

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("error=invalid", res.Headers.Location?.OriginalString ?? "");

        // DB unchanged
        var refreshed = await GetUserAsync(user.Username);
        Assert.True(refreshed!.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify("TempPassw0rd!", refreshed.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_Post_RejectsMismatchedConfirm()
    {
        var user = await SeedUserAsync("alice-mismatch", "TempPassw0rd!", mustChange: true);
        await SignInAsync(user, "TempPassw0rd!");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("current", "TempPassw0rd!"),
            new KeyValuePair<string, string>("new",     "NewPassw0rd!"),
            new KeyValuePair<string, string>("confirm", "Different0rd!"),
        });

        var res = await _client.PostAsync("/account/password/change", form);

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("error=mismatch", res.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task MustChangePasswordUser_IsForcedToChangePassword_AndCanCompleteIt()
    {
        // Full round-trip the way the browser would do it:
        //   1) login as user with MustChangePassword=true
        //   2) try to hit /apps -> middleware redirects to /account/change-password
        //   3) POST the new password
        //   4) hit /apps again -> now allowed (200)
        var user = await SeedUserAsync("alice-roundtrip", "TempPassw0rd!", mustChange: true);
        await SignInAsync(user, "TempPassw0rd!");

        var blocked = await _client.GetAsync("/apps");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Equal("/account/change-password", blocked.Headers.Location?.OriginalString);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("current", "TempPassw0rd!"),
            new KeyValuePair<string, string>("new",     "NewPassw0rd!"),
            new KeyValuePair<string, string>("confirm", "NewPassw0rd!"),
        });
        var submit = await _client.PostAsync("/account/password/change", form);
        Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
        Assert.Equal("/", submit.Headers.Location?.OriginalString);

        // After the cookie has been re-issued with MustChangePassword=false,
        // the user can reach a regular page.
        var allowed = await _client.GetAsync("/apps");
        Assert.True(allowed.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect,
            $"After password change, /apps should be reachable — got {(int)allowed.StatusCode}");
        if (allowed.StatusCode == HttpStatusCode.Redirect)
            Assert.NotEqual("/account/change-password", allowed.Headers.Location?.OriginalString);
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync(string username, string password, bool mustChange)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = new User
        {
            Username           = username,
            PasswordHash       = BCrypt.Net.BCrypt.HashPassword(password, 12),
            Role               = UserRole.Admin,
            IsActive           = true,
            MustChangePassword = mustChange,
        };
        return await users.CreateAsync(user);
    }

    private async Task<User?> GetUserAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        return await users.GetByUsernameAsync(username);
    }

    private async Task SignInAsync(User user, string password)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", user.Username),
            new KeyValuePair<string, string>("password", password),
        });
        var res = await _client.PostAsync("/account/login", form);
        Assert.True(res.StatusCode == HttpStatusCode.Redirect,
            $"Login should redirect — got {(int)res.StatusCode}");
        // The shared HttpClient already follows Set-Cookie because the
        // factory wires a CookieContainer per client by default.
    }
}
