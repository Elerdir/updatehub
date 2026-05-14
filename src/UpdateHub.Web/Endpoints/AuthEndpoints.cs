using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OtpNet;
using UpdateHub.Application.Services;

namespace UpdateHub.Web.Endpoints;

public static class AuthEndpoints
{
    public const string TwoFactorPendingScheme = "TwoFactorPending";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/account/login", async (HttpContext ctx, IConfiguration config,
            BruteForceProtectionService bruteForce, AuditService audit, SettingsService settings) =>
        {
            var ip        = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = ctx.Request.Headers.UserAgent.ToString();

            if (await bruteForce.IsBlockedAsync(ip))
            {
                await audit.LogAsync("LoginBlocked", actor: ip, ip: ip);
                ctx.Response.Redirect("/login?error=blocked");
                return;
            }

            var form     = await ctx.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var password = form["password"].ToString();

            var adminUser = config["UpdateHub:Admin:Username"] ?? "admin";
            // Check DB-stored hash first, fall back to config
            var dbHash    = await settings.GetAdminPasswordHashAsync();
            var adminHash = string.IsNullOrWhiteSpace(dbHash)
                ? (config["UpdateHub:Admin:PasswordHash"] ?? "")
                : dbHash;

            var valid = string.Equals(username, adminUser, StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrEmpty(adminHash)
                     && BCrypt.Net.BCrypt.Verify(password, adminHash);

            if (!valid)
            {
                await bruteForce.RecordFailureAsync(ip, userAgent);
                await audit.LogAsync("LoginFailed", actor: username, ip: ip,
                    details: $"Failed attempt from {ip}");
                ctx.Response.Redirect("/login?error=1");
                return;
            }

            await bruteForce.RecordSuccessAsync(ip);

            // If 2FA is enabled, issue a pending cookie and redirect to TOTP page
            if (await settings.IsTotpEnabledAsync())
            {
                var pendingClaims   = new[] { new Claim(ClaimTypes.Name, username) };
                var pendingIdentity = new ClaimsIdentity(pendingClaims, TwoFactorPendingScheme);
                await ctx.SignInAsync(TwoFactorPendingScheme, new ClaimsPrincipal(pendingIdentity));
                ctx.Response.Redirect("/login/totp");
                return;
            }

            await audit.LogAsync("LoginSuccess", actor: username, ip: ip);
            await SignInAdminAsync(ctx, username);
            ctx.Response.Redirect("/");
        }).AllowAnonymous().DisableAntiforgery().RequireRateLimiting("login");

        app.MapPost("/account/totp", async (HttpContext ctx, SettingsService settings, AuditService audit) =>
        {
            var result = await ctx.AuthenticateAsync(TwoFactorPendingScheme);
            if (!result.Succeeded) { ctx.Response.Redirect("/login"); return; }

            var form = await ctx.Request.ReadFormAsync();
            var code = form["code"].ToString().Replace(" ", "");
            var ip   = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var secret = await settings.GetTotpSecretAsync();
            if (string.IsNullOrEmpty(secret)) { ctx.Response.Redirect("/login"); return; }

            var totp  = new Totp(Base32Encoding.ToBytes(secret));
            var valid = totp.VerifyTotp(DateTime.UtcNow, code, out _, new VerificationWindow(1, 1));

            if (!valid)
            {
                await audit.LogAsync("LoginFailed2FA", actor: result.Principal!.Identity!.Name ?? "?", ip: ip);
                ctx.Response.Redirect("/login/totp?error=1");
                return;
            }

            await ctx.SignOutAsync(TwoFactorPendingScheme);

            var username = result.Principal!.Identity!.Name ?? "admin";
            await audit.LogAsync("LoginSuccess", actor: username, ip: ip);
            await SignInAdminAsync(ctx, username);
            ctx.Response.Redirect("/");
        }).AllowAnonymous().DisableAntiforgery().RequireRateLimiting("login");

        app.MapPost("/account/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ctx.Response.Redirect("/login");
        }).DisableAntiforgery();
    }

    private static Task SignInAdminAsync(HttpContext ctx, string username)
    {
        var claims   = new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
