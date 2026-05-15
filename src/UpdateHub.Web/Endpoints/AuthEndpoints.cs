using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OtpNet;
using UpdateHub.Application.Services;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Web.Endpoints;

public static class AuthEndpoints
{
    public const string TwoFactorPendingScheme = "TwoFactorPending";

    // Custom claims carried in the auth cookie
    public const string ClaimUserId           = "uh.uid";
    public const string ClaimSecurityStamp    = "uh.stamp";
    public const string ClaimMustChangePass   = "uh.mcp";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/account/login", async (HttpContext ctx,
            BruteForceProtectionService bruteForce, AuditService audit, UserService userSvc) =>
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

            var user = await userSvc.VerifyCredentialsAsync(username, password);
            if (user is null)
            {
                await bruteForce.RecordFailureAsync(ip, userAgent);
                await audit.LogAsync("LoginFailed", actor: username, ip: ip,
                    details: $"Failed attempt from {ip}");
                ctx.Response.Redirect("/login?error=1");
                return;
            }

            await bruteForce.RecordSuccessAsync(ip);

            // 2FA stage — only if this user has TOTP enabled
            if (user.TotpEnabled)
            {
                var pendingClaims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimUserId,     user.Id.ToString()),
                };
                var pendingIdentity = new ClaimsIdentity(pendingClaims, TwoFactorPendingScheme);
                await ctx.SignInAsync(TwoFactorPendingScheme, new ClaimsPrincipal(pendingIdentity));
                ctx.Response.Redirect("/login/totp");
                return;
            }

            await audit.LogAsync("LoginSuccess", actor: user.Username, ip: ip);
            await SignInAsync(ctx, user);
            ctx.Response.Redirect(user.MustChangePassword ? "/account/change-password" : "/");
        }).AllowAnonymous().DisableAntiforgery().RequireRateLimiting("login");

        app.MapPost("/account/totp", async (HttpContext ctx, UserService userSvc, AuditService audit) =>
        {
            var result = await ctx.AuthenticateAsync(TwoFactorPendingScheme);
            if (!result.Succeeded) { ctx.Response.Redirect("/login"); return; }

            var form  = await ctx.Request.ReadFormAsync();
            var code  = form["code"].ToString().Replace(" ", "");
            var ip    = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var idClaim = result.Principal!.FindFirst(ClaimUserId)?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) { ctx.Response.Redirect("/login"); return; }

            var user   = await userSvc.GetByIdAsync(userId);
            var secret = user is null ? null : await userSvc.GetTotpSecretAsync(userId);
            if (user is null || !user.IsActive || string.IsNullOrEmpty(secret))
            {
                ctx.Response.Redirect("/login"); return;
            }

            var totp  = new Totp(Base32Encoding.ToBytes(secret));
            var valid = totp.VerifyTotp(DateTime.UtcNow, code, out _, new VerificationWindow(1, 1));

            if (!valid)
            {
                await audit.LogAsync("LoginFailed2FA", actor: user.Username, ip: ip);
                ctx.Response.Redirect("/login/totp?error=1");
                return;
            }

            await ctx.SignOutAsync(TwoFactorPendingScheme);
            await audit.LogAsync("LoginSuccess", actor: user.Username, ip: ip);
            await SignInAsync(ctx, user);
            ctx.Response.Redirect(user.MustChangePassword ? "/account/change-password" : "/");
        }).AllowAnonymous().DisableAntiforgery().RequireRateLimiting("login");

        app.MapPost("/account/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ctx.Response.Redirect("/login");
        }).DisableAntiforgery();

        app.MapPost("/account/change-password", async (HttpContext ctx,
            UserService userSvc, AuditService audit) =>
        {
            var idClaim = ctx.User.FindFirst(ClaimUserId)?.Value;
            if (!Guid.TryParse(idClaim, out var userId))
            {
                ctx.Response.Redirect("/login");
                return;
            }

            var form     = await ctx.Request.ReadFormAsync();
            var current  = form["current"].ToString();
            var newPw    = form["new"].ToString();
            var confirm  = form["confirm"].ToString();

            string? err = null;
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(newPw))
                err = "missing";
            else if (newPw != confirm)
                err = "mismatch";
            else
            {
                try
                {
                    await userSvc.ChangeOwnPasswordAsync(userId, current, newPw);
                }
                catch (ArgumentException)
                {
                    err = "invalid";
                }
            }

            if (err is not null)
            {
                ctx.Response.Redirect($"/account/change-password?error={err}");
                return;
            }

            // Re-issue the cookie with refreshed claims (new SecurityStamp,
            // MustChangePassword=false) and bounce to the dashboard.
            var refreshed = await userSvc.GetByIdAsync(userId);
            if (refreshed is null) { ctx.Response.Redirect("/login"); return; }
            await SignInAsync(ctx, refreshed);
            ctx.Response.Redirect("/");
        }).RequireAuthorization().DisableAntiforgery();
    }

    private static Task SignInAsync(HttpContext ctx, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,     user.Username),
            new(ClaimTypes.Role,     user.Role.ToString()),
            new(ClaimUserId,         user.Id.ToString()),
            new(ClaimSecurityStamp,  user.SecurityStamp),
        };
        if (user.MustChangePassword)
            claims.Add(new Claim(ClaimMustChangePass, "1"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
