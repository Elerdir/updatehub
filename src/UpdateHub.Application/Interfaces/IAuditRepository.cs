using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface IAuditRepository
{
    Task LogAsync(AuditEntry entry);
    Task<List<AuditEntry>> GetRecentAsync(int count = 200);
}
