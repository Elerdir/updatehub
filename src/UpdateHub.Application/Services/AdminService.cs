using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Application.Services;

public class AdminService(
    IAppRepository appRepo,
    IReleaseRepository releaseRepo,
    IArtifactRepository artifactRepo,
    IArtifactStorage storage)
{
    public Task<List<App>> GetAppsAsync() => appRepo.GetAllWithReleasesAsync();

    public Task<App?> GetAppAsync(string slug) => appRepo.GetBySlugAsync(slug);

    public Task<Release?> GetReleaseAsync(Guid releaseId) => releaseRepo.GetByIdAsync(releaseId);

    public Task<App> CreateAppAsync(string slug, string name, string? description) =>
        appRepo.CreateAsync(new App
        {
            Slug        = slug.Trim().ToLower(),
            Name        = name.Trim(),
            Description = description?.Trim()
        });

    public async Task UpdateAppAsync(Guid id, string name, string? description)
    {
        var app = await appRepo.GetAllWithReleasesAsync()
            .ContinueWith(t => t.Result.FirstOrDefault(a => a.Id == id))
            ?? throw new InvalidOperationException("App not found");
        app.Name        = name.Trim();
        app.Description = description?.Trim();
        await appRepo.UpdateAsync(app);
    }

    public async Task DeleteAppAsync(Guid id)
    {
        var app = (await appRepo.GetAllWithReleasesAsync()).FirstOrDefault(a => a.Id == id);
        if (app is not null) await appRepo.DeleteAsync(app);
    }

    public Task<Release> CreateReleaseAsync(
        Guid appId, string version, ReleaseChannel channel, string? notes, bool mandatory) =>
        releaseRepo.CreateAsync(new Release
        {
            AppId        = appId,
            Version      = version.Trim(),
            Channel      = channel,
            ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IsMandatory  = mandatory
        });

    public async Task PublishReleaseAsync(Guid releaseId)
    {
        var r = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");

        // Archive any previously published release for the same app + channel
        var older = await releaseRepo.GetPublishedByAppChannelAsync(r.AppId, r.Channel, releaseId);
        foreach (var old in older)
        {
            old.Status = ReleaseStatus.Archived;
            await releaseRepo.UpdateAsync(old);
        }

        r.Status      = ReleaseStatus.Published;
        r.PublishedAt = DateTime.UtcNow;
        await releaseRepo.UpdateAsync(r);
    }

    public async Task ArchiveReleaseAsync(Guid releaseId)
    {
        var r = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");
        r.Status = ReleaseStatus.Archived;
        await releaseRepo.UpdateAsync(r);
    }

    public async Task DeleteReleaseAsync(Guid releaseId)
    {
        var r = await releaseRepo.GetByIdAsync(releaseId);
        if (r is not null) await releaseRepo.DeleteAsync(r);
    }

    public async Task<Artifact> AddArtifactAsync(
        Guid releaseId, string platform, string arch,
        Stream stream, string fileName, string? signature)
    {
        var release = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");

        var (storedPath, sha256, fileSize) = await storage.StoreAsync(
            stream, fileName, release.App.Slug, release.Version);

        return await artifactRepo.CreateAsync(new Artifact
        {
            ReleaseId     = releaseId,
            Platform      = platform.Trim().ToLower(),
            Architecture  = arch.Trim().ToLower(),
            FileName      = Path.GetFileName(fileName),
            StoredPath    = storedPath,
            Sha256        = sha256,
            Signature     = string.IsNullOrWhiteSpace(signature) ? null : signature.Trim(),
            FileSizeBytes = fileSize
        });
    }

    public async Task DeleteArtifactAsync(Guid artifactId)
    {
        var a = await artifactRepo.GetByIdAsync(artifactId);
        if (a is null) return;
        storage.Delete(a.StoredPath);
        await artifactRepo.DeleteAsync(a);
    }

    public async Task<(int apps, int published, long downloads)> GetStatsAsync()
    {
        var all = await appRepo.GetAllWithReleasesAsync();
        var apps      = all.Count;
        var published = all.SelectMany(a => a.Releases)
                           .Count(r => r.Status == ReleaseStatus.Published);
        var downloads = all.SelectMany(a => a.Releases)
                           .SelectMany(r => r.Artifacts)
                           .Sum(a => a.DownloadCount);
        return (apps, published, downloads);
    }
}
