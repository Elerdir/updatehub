using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Application.Services;

public class AdminService(
    IAppRepository          appRepo,
    IReleaseRepository      releaseRepo,
    IArtifactRepository     artifactRepo,
    IArtifactStorage        storage,
    IWebhookService         webhook,
    AuditService            audit,
    EmailNotificationService email)
{
    public Task<List<App>> GetAppsAsync() => appRepo.GetAllWithReleasesAsync();

    public Task<App?> GetAppAsync(string slug) => appRepo.GetBySlugAsync(slug);

    public Task<Release?> GetReleaseAsync(Guid releaseId) => releaseRepo.GetByIdAsync(releaseId);

    public async Task<App> CreateAppAsync(string slug, string name, string? description)
    {
        var app = await appRepo.CreateAsync(new App
        {
            Slug        = slug.Trim().ToLower(),
            Name        = name.Trim(),
            Description = description?.Trim()
        });
        await audit.LogAsync("CreateApp", entityType: "App", entityId: app.Id.ToString(), details: app.Slug);
        return app;
    }

    public async Task UpdateAppAsync(Guid id, string name, string? description)
    {
        var app = await appRepo.GetAllWithReleasesAsync()
            .ContinueWith(t => t.Result.FirstOrDefault(a => a.Id == id))
            ?? throw new InvalidOperationException("App not found");
        app.Name        = name.Trim();
        app.Description = description?.Trim();
        await appRepo.UpdateAsync(app);
        await audit.LogAsync("UpdateApp", entityType: "App", entityId: id.ToString(), details: name.Trim());
    }

    public async Task DeleteAppAsync(Guid id)
    {
        var app = (await appRepo.GetAllWithReleasesAsync()).FirstOrDefault(a => a.Id == id);
        if (app is null) return;
        await appRepo.DeleteAsync(app);
        await audit.LogAsync("DeleteApp", entityType: "App", entityId: id.ToString(), details: app.Slug);
    }

    public async Task<Release> CreateReleaseAsync(
        Guid appId, string version, ReleaseChannel channel, string? notes, bool mandatory)
    {
        var release = await releaseRepo.CreateAsync(new Release
        {
            AppId        = appId,
            Version      = version.Trim(),
            Channel      = channel,
            ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IsMandatory  = mandatory
        });
        await audit.LogAsync("CreateRelease", entityType: "Release", entityId: release.Id.ToString(),
            details: $"{version.Trim()} / {channel}");
        return release;
    }

    public async Task PublishReleaseAsync(Guid releaseId)
    {
        var r = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");

        var older = await releaseRepo.GetPublishedByAppChannelAsync(r.AppId, r.Channel, releaseId);
        foreach (var old in older)
        {
            old.Status = ReleaseStatus.Archived;
            await releaseRepo.UpdateAsync(old);
        }

        r.Status      = ReleaseStatus.Published;
        r.PublishedAt = DateTime.UtcNow;
        await releaseRepo.UpdateAsync(r);

        await audit.LogAsync("PublishRelease", entityType: "Release", entityId: releaseId.ToString(),
            details: $"{r.App.Slug} v{r.Version} ({r.Channel})");

        var channel = r.Channel.ToString().ToLower();
        await webhook.NotifyPublishedAsync(r.App.Slug, r.Version, r.ReleaseNotes, channel);
        await email.SendReleasePublishedAsync(r.App.Slug, r.Version, channel);
    }

    public async Task ArchiveReleaseAsync(Guid releaseId)
    {
        var r = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");
        r.Status = ReleaseStatus.Archived;
        await releaseRepo.UpdateAsync(r);
        await audit.LogAsync("ArchiveRelease", entityType: "Release", entityId: releaseId.ToString());
    }

    public async Task DeleteReleaseAsync(Guid releaseId)
    {
        var r = await releaseRepo.GetByIdAsync(releaseId);
        if (r is null) return;
        await releaseRepo.DeleteAsync(r);
        await audit.LogAsync("DeleteRelease", entityType: "Release", entityId: releaseId.ToString());
    }

    public async Task<Artifact> AddArtifactAsync(
        Guid releaseId, string platform, string arch,
        Stream stream, string fileName, string? signature)
    {
        var release = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");

        var (storedPath, sha256, fileSize) = await storage.StoreAsync(
            stream, fileName, release.App.Slug, release.Version);

        var artifact = await artifactRepo.CreateAsync(new Artifact
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

        await audit.LogAsync("UploadArtifact", entityType: "Artifact", entityId: artifact.Id.ToString(),
            details: $"{Path.GetFileName(fileName)} ({platform}/{arch})");
        return artifact;
    }

    public async Task DeleteArtifactAsync(Guid artifactId)
    {
        var a = await artifactRepo.GetByIdAsync(artifactId);
        if (a is null) return;
        storage.Delete(a.StoredPath);
        await artifactRepo.DeleteAsync(a);
        await audit.LogAsync("DeleteArtifact", entityType: "Artifact", entityId: artifactId.ToString(),
            details: a.FileName);
    }

    public async Task<(int apps, int published, long downloads)> GetStatsAsync()
    {
        var all       = await appRepo.GetAllWithReleasesAsync();
        var apps      = all.Count;
        var published = all.SelectMany(a => a.Releases)
                           .Count(r => r.Status == ReleaseStatus.Published);
        var downloads = all.SelectMany(a => a.Releases)
                           .SelectMany(r => r.Artifacts)
                           .Sum(a => a.DownloadCount);
        return (apps, published, downloads);
    }

    // ── Per-app CI token ──────────────────────────────────────────────────────

    public async Task<string> RotateAppCiTokenAsync(Guid appId)
    {
        var app = (await appRepo.GetAllWithReleasesAsync()).FirstOrDefault(a => a.Id == appId)
            ?? throw new InvalidOperationException("App not found");
        app.CiToken = GenerateToken();
        await appRepo.UpdateAsync(app);
        await audit.LogAsync("RotateAppCiToken", entityType: "App", entityId: appId.ToString(), details: app.Slug);
        return app.CiToken;
    }

    public async Task ClearAppCiTokenAsync(Guid appId)
    {
        var app = (await appRepo.GetAllWithReleasesAsync()).FirstOrDefault(a => a.Id == appId)
            ?? throw new InvalidOperationException("App not found");
        app.CiToken = null;
        await appRepo.UpdateAsync(app);
        await audit.LogAsync("ClearAppCiToken", entityType: "App", entityId: appId.ToString(), details: app.Slug);
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
