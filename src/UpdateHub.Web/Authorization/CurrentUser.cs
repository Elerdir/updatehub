using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using UpdateHub.Application.Interfaces;
using UpdateHub.Web.Endpoints;

namespace UpdateHub.Web.Authorization;

/// <summary>
/// <see cref="ICurrentUser"/> implementation that resolves the principal from
/// both worlds:
///
///   * Minimal API / Razor SSR — <see cref="IHttpContextAccessor"/> gives the
///     live <c>HttpContext.User</c>.
///   * Blazor InteractiveServer circuits — after hydration the original
///     HttpContext is gone, so we fall back to
///     <see cref="AuthenticationStateProvider"/> which the circuit keeps
///     populated as long as the user is signed in.
///
/// Without this dual lookup, every <see cref="Application.Authorization.RoleGuard"/>
/// check called from a Razor click handler would fail because
/// <c>HttpContextAccessor.HttpContext</c> is null on the SignalR thread.
/// </summary>
public class CurrentUser(
    IHttpContextAccessor accessor,
    AuthenticationStateProvider? authState = null) : ICurrentUser
{
    private ClaimsPrincipal? Principal
    {
        get
        {
            // Prefer the live HttpContext when present (real request thread).
            var ctxUser = accessor.HttpContext?.User;
            if (ctxUser?.Identity?.IsAuthenticated == true) return ctxUser;

            // Fall back to the Blazor circuit's cached principal — this is
            // what AuthorizeView reads, so it stays consistent with the UI.
            if (authState is null) return ctxUser;
            var state = authState.GetAuthenticationStateAsync().GetAwaiter().GetResult();
            return state.User;
        }
    }

    public bool    IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public string? Name            => Principal?.Identity?.Name;

    public Guid? Id
    {
        get
        {
            var s = Principal?.FindFirst(AuthEndpoints.ClaimUserId)?.Value;
            return Guid.TryParse(s, out var g) ? g : null;
        }
    }

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
