using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Infrastructure.Persistence;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class LoginAttemptRepository(AppDbContext db) : ILoginAttemptRepository
{
    public Task<LoginAttempt?> GetByIpAsync(string ip) =>
        db.LoginAttempts.FirstOrDefaultAsync(a => a.IpAddress == ip);

    public async Task UpsertAsync(LoginAttempt attempt)
    {
        var existing = await db.LoginAttempts.FindAsync(attempt.IpAddress);
        if (existing is null)
            db.LoginAttempts.Add(attempt);
        else
            db.Entry(existing).CurrentValues.SetValues(attempt);
        await db.SaveChangesAsync();
    }

    public Task<List<LoginAttempt>> GetAllAsync() =>
        db.LoginAttempts
          .OrderByDescending(a => a.IsBlocked)
          .ThenByDescending(a => a.LastAttemptAt)
          .ToListAsync();

    public async Task DeleteAsync(string ip)
    {
        var a = await db.LoginAttempts.FindAsync(ip);
        if (a is not null) db.LoginAttempts.Remove(a);
        await db.SaveChangesAsync();
    }
}
