using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class PersonalAccessTokenRepository(AppDbContext db) : IPersonalAccessTokenRepository
{
    public Task<List<PersonalAccessToken>> GetForUserAsync(Guid userId) =>
        db.PersonalAccessTokens
          .Where(t => t.UserId == userId)
          .OrderByDescending(t => t.CreatedAt)
          .ToListAsync();

    public Task<PersonalAccessToken?> GetByHashAsync(string tokenHash) =>
        db.PersonalAccessTokens
          .Include(t => t.User)
          .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task CreateAsync(PersonalAccessToken token)
    {
        db.PersonalAccessTokens.Add(token);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PersonalAccessToken token)
    {
        db.PersonalAccessTokens.Update(token);
        await db.SaveChangesAsync();
    }
}
