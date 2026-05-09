using Microsoft.EntityFrameworkCore;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Infrastructure.Persistence;

namespace UpdateHub.Infrastructure.Persistence.Repositories;

public class ArtifactRepository(AppDbContext db) : IArtifactRepository
{
    public Task<Artifact?> GetByIdAsync(Guid id) =>
        db.Artifacts.FirstOrDefaultAsync(a => a.Id == id);

    public async Task<Artifact> CreateAsync(Artifact artifact)
    {
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();
        return artifact;
    }

    public async Task DeleteAsync(Artifact artifact)
    {
        db.Artifacts.Remove(artifact);
        await db.SaveChangesAsync();
    }
}
