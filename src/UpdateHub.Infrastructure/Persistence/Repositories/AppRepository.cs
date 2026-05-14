using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Infrastructure.Persistence;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class AppRepository(AppDbContext db) : IAppRepository
{
    public Task<List<App>> GetAllWithReleasesAsync() =>
        db.Apps
          .Include(a => a.Releases).ThenInclude(r => r.Artifacts)
          .OrderBy(a => a.Name)
          .ToListAsync();

    public Task<App?> GetByIdAsync(Guid id) =>
        db.Apps
          .Include(a => a.Releases).ThenInclude(r => r.Artifacts)
          .FirstOrDefaultAsync(a => a.Id == id);

    public Task<App?> GetBySlugAsync(string slug) =>
        db.Apps
          .Include(a => a.Releases).ThenInclude(r => r.Artifacts)
          .FirstOrDefaultAsync(a => a.Slug == slug);

    public async Task<App> CreateAsync(App app)
    {
        db.Apps.Add(app);
        await db.SaveChangesAsync();
        return app;
    }

    public async Task UpdateAsync(App app)
    {
        db.Apps.Update(app);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(App app)
    {
        db.Apps.Remove(app);
        await db.SaveChangesAsync();
    }
}
