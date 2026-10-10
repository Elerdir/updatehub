using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Services;

public class AuditService(IAuditRepository repo)
{
    public Task LogAsync(
        string  action,
        string  actor      = "admin",
        string? entityType = null,
        string? entityId   = null,
        string? details    = null,
        string? ip         = null)
        => repo.LogAsync(new AuditEntry
        {
            Action     = action,
            Actor      = actor,
            EntityType = entityType,
            EntityId   = entityId,
            Details    = details,
            IpAddress  = ip
        });

    public Task<List<AuditEntry>> GetRecentAsync(int count = 200)
        => repo.GetRecentAsync(count);
}
