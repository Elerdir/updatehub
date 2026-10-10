using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository(AppDbContext db) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task CreateAsync(PasswordResetToken token)
    {
        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PasswordResetToken token)
    {
        db.PasswordResetTokens.Update(token);
        await db.SaveChangesAsync();
    }

    public async Task InvalidateAllForUserAsync(Guid userId)
    {
        var live = await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ToListAsync();
        foreach (var t in live) t.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
