using UpdateHub.Application.Interfaces;

namespace UpdateHub.Application.Authorization;

/// <summary>
/// Defense-in-depth: every write method on a service that runs from the
/// admin UI should call <see cref="Require"/> at the start. The Razor
/// <c>AuthorizeView</c> guards hide the buttons; this guard makes sure that
/// even a hand-crafted request from a Viewer (or an unauthenticated user)
/// cannot mutate state.
/// </summary>
public static class RoleGuard
{
    public static void Require(ICurrentUser user, params string[] roles)
    {
        if (!user.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication required.");
        foreach (var r in roles)
            if (user.IsInRole(r)) return;
        throw new UnauthorizedAccessException(
            $"Requires one of: {string.Join(", ", roles)}");
    }
}
