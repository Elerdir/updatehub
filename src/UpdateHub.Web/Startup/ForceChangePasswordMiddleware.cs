using Microsoft.AspNetCore.Authentication;
using UpdateHub.Application.Services;
using UpdateHub.Web.Endpoints;

namespace UpdateHub.Web.Startup;

/// <summary>
/// If the authenticated user has the MustChangePassword claim, redirect every
/// request (except the change-password page itself, logout, static assets, and
/// health) to /account/change-password. Also enforces the SecurityStamp by
/// signing the user out if it no longer matches the DB (admin reset, disable).
/// </summary>
public static class ForceChangePasswordMiddleware
{
    public static IApplicationBuilder UseForceChangePassword(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var user = ctx.User;
            if (user.Identity?.IsAuthenticated != true) { await next(); return; }

            var path = ctx.Request.Path.Value ?? "";

            // Always-allowed paths so the user can complete the flow or sign out
            if (path.StartsWith("/account/change-password", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/account/logout", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".js",  StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // SecurityStamp validation — sign out if the DB stamp has changed
            // (admin reset password or disabled the account).
            var stampClaim = user.FindFirst(AuthEndpoints.ClaimSecurityStamp)?.Value;
            var idClaim    = user.FindFirst(AuthEndpoints.ClaimUserId)?.Value;
            if (Guid.TryParse(idClaim, out var userId) && stampClaim is not null)
            {
                var userSvc = ctx.RequestServices.GetRequiredService<UserService>();
                var dbUser  = await userSvc.GetByIdAsync(userId);
                if (dbUser is null || !dbUser.IsActive || dbUser.SecurityStamp != stampClaim)
                {
                    await ctx.SignOutAsync();
                    ctx.Response.Redirect("/login");
                    return;
                }
            }

            if (user.HasClaim(AuthEndpoints.ClaimMustChangePass, "1"))
            {
                ctx.Response.Redirect("/account/change-password");
                return;
            }

            await next();
        });
}
