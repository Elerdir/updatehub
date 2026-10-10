using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Infrastructure.Persistence;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class AuditRepository(AppDbContext db) : IAuditRepository
{
    public async Task LogAsync(AuditEntry entry)
    {
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public Task<List<AuditEntry>> GetRecentAsync(int count = 200) =>
        db.AuditEntries
          .OrderByDescending(e => e.OccurredAt)
          .Take(count)
          .ToListAsync();
}
