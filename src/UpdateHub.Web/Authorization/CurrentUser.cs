using UpdateHub.Application.Interfaces;
using UpdateHub.Web.Endpoints;

namespace UpdateHub.Web.Authorization;

/// <summary>HttpContext-backed implementation of <see cref="ICurrentUser"/>.</summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public string? Name         => Principal?.Identity?.Name;

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
