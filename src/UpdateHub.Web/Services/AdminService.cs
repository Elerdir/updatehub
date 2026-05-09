using Microsoft.EntityFrameworkCore;
using UpdateHub.Web.Data;
using UpdateHub.Web.Data.Entities;

namespace UpdateHub.Web.Services;

public class AdminService(AppDbContext db)
{
    public async Task<List<App>> GetAppsAsync() =>
        await db.Apps
            .Include(a => a.Releases).ThenInclude(r => r.Artifacts)
            .OrderBy(a => a.Name)
            .ToListAsync();

    public async Task<App?> GetAppAsync(string slug) =>
        await db.Apps
            .Include(a => a.Releases).ThenInclude(r => r.Artifacts)
            .FirstOrDefaultAsync(a => a.Slug == slug);

    public async Task<Release?> GetReleaseAsync(Guid releaseId) =>
        await db.Releases
            .Include(r => r.App)
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == releaseId);

    public async Task<App> CreateAppAsync(string slug, string name, string? description)
    {
        var app = new App { Slug = slug.Trim().ToLower(), Name = name.Trim(), Description = description?.Trim() };
        db.Apps.Add(app);
        await db.SaveChangesAsync();
        return app;
    }

    public async Task UpdateAppAsync(Guid id, string name, string? description)
    {
        var app = await db.Apps.FindAsync(id) ?? throw new InvalidOperationException("App not found");
        app.Name = name.Trim();
        app.Description = description?.Trim();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAppAsync(Guid id)
    {
        var app = await db.Apps.FindAsync(id);
        if (app is not null) { db.Apps.Remove(app); await db.SaveChangesAsync(); }
    }

    public async Task<Release> CreateReleaseAsync(
        Guid appId, string version, ReleaseChannel channel, string? notes, bool mandatory)
    {
        var release = new Release
        {
            AppId = appId,
            Version = version.Trim(),
            Channel = channel,
            ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IsMandatory = mandatory
        };
        db.Releases.Add(release);
        await db.SaveChangesAsync();
        return release;
    }

    public async Task PublishReleaseAsync(Guid releaseId)
    {
        var r = await db.Releases.FindAsync(releaseId) ?? throw new InvalidOperationException("Release not found");
        r.Status = ReleaseStatus.Published;
        r.PublishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveReleaseAsync(Guid releaseId)
    {
        var r = await db.Releases.FindAsync(releaseId) ?? throw new InvalidOperationException("Release not found");
        r.Status = ReleaseStatus.Archived;
        await db.SaveChangesAsync();
    }

    public async Task DeleteReleaseAsync(Guid releaseId)
    {
        var r = await db.Releases.FindAsync(releaseId);
        if (r is not null) { db.Releases.Remove(r); await db.SaveChangesAsync(); }
    }

    public async Task<Artifact> AddArtifactAsync(
        Guid releaseId, string platform, string arch,
        Stream stream, string fileName, string? signature,
        ArtifactStorageService storage)
    {
        var release = await db.Releases.Include(r => r.App).FirstOrDefaultAsync(r => r.Id == releaseId)
            ?? throw new InvalidOperationException("Release not found");

        var (storedPath, sha256, fileSize) = await storage.StoreAsync(
            stream, fileName, release.App.Slug, release.Version);

        var artifact = new Artifact
        {
            ReleaseId = releaseId,
            Platform = platform.Trim().ToLower(),
            Architecture = arch.Trim().ToLower(),
            FileName = Path.GetFileName(fileName),
            StoredPath = storedPath,
            Sha256 = sha256,
            Signature = string.IsNullOrWhiteSpace(signature) ? null : signature.Trim(),
            FileSizeBytes = fileSize
        };
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();
        return artifact;
    }

    public async Task DeleteArtifactAsync(Guid artifactId, ArtifactStorageService storage)
    {
        var a = await db.Artifacts.FindAsync(artifactId);
        if (a is not null)
        {
            storage.Delete(a.StoredPath);
            db.Artifacts.Remove(a);
            await db.SaveChangesAsync();
        }
    }

    public async Task<(int apps, int published)> GetStatsAsync()
    {
        var apps = await db.Apps.CountAsync();
        var published = await db.Releases.CountAsync(r => r.Status == ReleaseStatus.Published);
        return (apps, published);
    }
}
