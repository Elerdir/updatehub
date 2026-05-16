using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using UpdateHub.Infrastructure.Persistence;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class ReleaseRepository(AppDbContext db) : IReleaseRepository
{
    public Task<Release?> GetByIdAsync(Guid id) =>
        db.Releases
          .Include(r => r.App)
          .Include(r => r.Artifacts)
          .FirstOrDefaultAsync(r => r.Id == id);

    public Task<Release?> GetLatestPublishedAsync(string appSlug, ReleaseChannel channel) =>
        db.Releases
          .Include(r => r.Artifacts)
          .Include(r => r.App)
          .Where(r => r.App.Slug == appSlug
                   && r.Status == ReleaseStatus.Published
                   && r.Channel == channel)
          .OrderByDescending(r => r.PublishedAt)
          .FirstOrDefaultAsync();

    public Task<List<Release>> GetAllPublishedAsync(string appSlug, ReleaseChannel channel) =>
        db.Releases
          .Include(r => r.Artifacts)
          .Include(r => r.App)
          .Where(r => r.App.Slug == appSlug
                   && r.Status == ReleaseStatus.Published
                   && r.Channel == channel)
          .OrderByDescending(r => r.PublishedAt)
          .ToListAsync();

    public Task<List<Release>> GetPublishedByAppChannelAsync(
        Guid appId, ReleaseChannel channel, Guid exceptId) =>
        db.Releases
          .Where(r => r.AppId == appId
                   && r.Channel == channel
                   && r.Status == ReleaseStatus.Published
                   && r.Id != exceptId)
          .ToListAsync();

    public async Task<Release> CreateAsync(Release release)
    {
        db.Releases.Add(release);
        await db.SaveChangesAsync();
        return release;
    }

    public async Task UpdateAsync(Release release)
    {
        db.Releases.Update(release);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Release release)
    {
        db.Releases.Remove(release);
        await db.SaveChangesAsync();
    }
}
