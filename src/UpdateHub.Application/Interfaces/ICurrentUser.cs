namespace UpdateHub.Application.Interfaces;

/// <summary>
/// Abstraction over the request's authenticated user so Application services
/// (which must stay framework-agnostic) can perform authorization checks
/// without taking a hard dependency on HttpContext.
/// </summary>
public interface ICurrentUser
{
    bool    IsAuthenticated { get; }
    string? Name            { get; }
    Guid?   Id              { get; }
    bool    IsInRole(string role);
}
