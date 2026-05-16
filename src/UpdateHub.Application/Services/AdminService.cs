using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Application.Authorization;
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
    EmailNotificationService email,
    INotificationQueue      notifications,
    ICurrentUser            currentUser)
{
    private const string Admin   = "Admin";
    private const string Manager = "Manager";
    public Task<List<App>> GetAppsAsync() => appRepo.GetAllWithReleasesAsync();

    public Task<App?> GetAppAsync(string slug) => appRepo.GetBySlugAsync(slug);

    public Task<Release?> GetReleaseAsync(Guid releaseId) => releaseRepo.GetByIdAsync(releaseId);

    public async Task<App> CreateAppAsync(string slug, string name, string? description)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
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
        RoleGuard.Require(currentUser, Admin, Manager);
        var app = await appRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException("App not found");
        app.Name        = name.Trim();
        app.Description = description?.Trim();
        await appRepo.UpdateAsync(app);
        await audit.LogAsync("UpdateApp", entityType: "App", entityId: id.ToString(), details: name.Trim());
    }

    public async Task DeleteAppAsync(Guid id)
    {
        RoleGuard.Require(currentUser, Admin);
        var app = await appRepo.GetByIdAsync(id);
        if (app is null) return;
        await appRepo.DeleteAsync(app);
        await audit.LogAsync("DeleteApp", entityType: "App", entityId: id.ToString(), details: app.Slug);
    }

    public async Task<Release> CreateReleaseAsync(
        Guid appId, string version, ReleaseChannel channel, string? notes, bool mandatory,
        string? minFromVersion = null)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
        var release = await releaseRepo.CreateAsync(new Release
        {
            AppId          = appId,
            Version        = version.Trim(),
            Channel        = channel,
            ReleaseNotes   = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IsMandatory    = mandatory,
            MinFromVersion = string.IsNullOrWhiteSpace(minFromVersion) ? null : minFromVersion.Trim(),
        });
        await audit.LogAsync("CreateRelease", entityType: "Release", entityId: release.Id.ToString(),
            details: $"{version.Trim()} / {channel}");
        return release;
    }

    public async Task SetMinFromVersionAsync(Guid releaseId, string? minFromVersion)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
        var r = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");
        var normalized = string.IsNullOrWhiteSpace(minFromVersion) ? null : minFromVersion.Trim();
        if (r.MinFromVersion == normalized) return;
        r.MinFromVersion = normalized;
        await releaseRepo.UpdateAsync(r);
        await audit.LogAsync("SetMinFromVersion", entityType: "Release",
            entityId: releaseId.ToString(),
            details: $"{r.Version} → {(normalized ?? "(none)")}");
    }

    public async Task PublishReleaseAsync(Guid releaseId)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
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

        // Webhook + email run on the background queue — publishing returns immediately
        // even if SMTP/the webhook endpoint is slow or unreachable.
        var channel = r.Channel.ToString().ToLower();
        var slug    = r.App.Slug;
        var version = r.Version;
        var notes   = r.ReleaseNotes;
        notifications.Enqueue(async (sp, _) =>
        {
            var wh = sp.GetRequiredService<IWebhookService>();
            var em = sp.GetRequiredService<EmailNotificationService>();
            await wh.NotifyPublishedAsync(slug, version, notes, channel);
            await em.SendReleasePublishedAsync(slug, version, channel);
        });
    }

    public async Task ArchiveReleaseAsync(Guid releaseId)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
        var r = await releaseRepo.GetByIdAsync(releaseId)
            ?? throw new InvalidOperationException("Release not found");
        r.Status = ReleaseStatus.Archived;
        await releaseRepo.UpdateAsync(r);
        await audit.LogAsync("ArchiveRelease", entityType: "Release", entityId: releaseId.ToString());
    }

    public async Task DeleteReleaseAsync(Guid releaseId)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
        var r = await releaseRepo.GetByIdAsync(releaseId);
        if (r is null) return;
        await releaseRepo.DeleteAsync(r);
        await audit.LogAsync("DeleteRelease", entityType: "Release", entityId: releaseId.ToString());
    }

    public async Task<Artifact> AddArtifactAsync(
        Guid releaseId, string platform, string arch,
        Stream stream, string fileName, string? signature)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
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

    /// <summary>
    /// CI upload path: finds an existing draft/release for the given version+channel
    /// or creates a new Draft, then stores the artifact. Everything is audited with
    /// actor "ci" so token-based uploads show up in the audit log.
    /// </summary>
    public async Task<(Release release, Artifact artifact)> IngestCiUploadAsync(
        App app, string version, ReleaseChannel channel, string? notes, bool mandatory,
        Stream stream, string fileName, string platform, string arch, string? signature,
        string? minFromVersion = null)
    {
        var release = app.Releases.FirstOrDefault(r =>
            r.Version == version.Trim() && r.Channel == channel);

        if (release is null)
        {
            release = await releaseRepo.CreateAsync(new Release
            {
                AppId          = app.Id,
                Version        = version.Trim(),
                Channel        = channel,
                Status         = ReleaseStatus.Draft,
                ReleaseNotes   = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                IsMandatory    = mandatory,
                MinFromVersion = string.IsNullOrWhiteSpace(minFromVersion) ? null : minFromVersion.Trim(),
            });
            await audit.LogAsync("CreateRelease", actor: "ci", entityType: "Release",
                entityId: release.Id.ToString(), details: $"{version.Trim()} / {channel}");
        }

        var (storedPath, sha256, fileSize) = await storage.StoreAsync(
            stream, fileName, app.Slug, version);

        var artifact = await artifactRepo.CreateAsync(new Artifact
        {
            ReleaseId     = release.Id,
            Platform      = platform.Trim().ToLower(),
            Architecture  = arch.Trim().ToLower(),
            FileName      = Path.GetFileName(fileName),
            StoredPath    = storedPath,
            Sha256        = sha256,
            Signature     = string.IsNullOrWhiteSpace(signature) ? null : signature.Trim(),
            FileSizeBytes = fileSize
        });

        await audit.LogAsync("UploadArtifact", actor: "ci", entityType: "Artifact",
            entityId: artifact.Id.ToString(), details: $"{Path.GetFileName(fileName)} ({platform}/{arch})");

        return (release, artifact);
    }

    public async Task DeleteArtifactAsync(Guid artifactId)
    {
        RoleGuard.Require(currentUser, Admin, Manager);
        var a = await artifactRepo.GetByIdAsync(artifactId);
        if (a is null) return;
        storage.Delete(a.StoredPath);
        await artifactRepo.DeleteAsync(a);
        await audit.LogAsync("DeleteArtifact", entityType: "Artifact", entityId: artifactId.ToString(),
            details: a.FileName);
    }

    public async Task<List<(string Slug, string Name, long Bytes, int Artifacts)>> GetStoragePerAppAsync()
    {
        var apps = await appRepo.GetAllWithReleasesAsync();
        return apps.Select(a =>
        {
            var artifacts = a.Releases.SelectMany(r => r.Artifacts).ToList();
            var bytes = artifacts.Sum(x => x.FileSizeBytes);
            return (a.Slug, a.Name, bytes, artifacts.Count);
        }).OrderByDescending(x => x.bytes).ToList();
    }

    /// <summary>
    /// Deletes every artifact attached to releases archived more than
    /// <paramref name="olderThanDays"/> days ago. Releases themselves are
    /// preserved (only their files go) so the audit / metadata stays intact.
    /// Returns count + bytes freed.
    /// </summary>
    public async Task<(int artifactsRemoved, long bytesFreed)> CleanupArchivedAsync(int olderThanDays)
    {
        RoleGuard.Require(currentUser, Admin);
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        var apps   = await appRepo.GetAllWithReleasesAsync();

        var stale = apps.SelectMany(a => a.Releases)
            .Where(r => r.Status == ReleaseStatus.Archived
                     && (r.PublishedAt ?? r.CreatedAt) < cutoff)
            .SelectMany(r => r.Artifacts)
            .ToList();

        long freed = 0;
        foreach (var a in stale)
        {
            try { storage.Delete(a.StoredPath); } catch { /* best-effort */ }
            await artifactRepo.DeleteAsync(a);
            freed += a.FileSizeBytes;
        }

        if (stale.Count > 0)
        {
            await audit.LogAsync("CleanupArchived",
                entityType: "Storage",
                details: $"{stale.Count} artifacts / {freed} bytes (older than {olderThanDays}d)");
        }

        return (stale.Count, freed);
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
        RoleGuard.Require(currentUser, Admin);
        var app = await appRepo.GetByIdAsync(appId)
            ?? throw new InvalidOperationException("App not found");
        app.CiToken = GenerateToken();
        await appRepo.UpdateAsync(app);
        await audit.LogAsync("RotateAppCiToken", entityType: "App", entityId: appId.ToString(), details: app.Slug);
        return app.CiToken;
    }

    public async Task SetAppWebhookUrlAsync(Guid appId, string? url)
    {
        RoleGuard.Require(currentUser, Admin);
        var app = await appRepo.GetByIdAsync(appId)
            ?? throw new InvalidOperationException("App not found");
        var normalized = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        if (app.WebhookUrl == normalized) return;
        app.WebhookUrl = normalized;
        await appRepo.UpdateAsync(app);
        await audit.LogAsync("SetAppWebhookUrl", entityType: "App",
            entityId: appId.ToString(),
            details: $"{app.Slug} → {(normalized ?? "(none)")}");
    }

    public async Task ClearAppCiTokenAsync(Guid appId)
    {
        RoleGuard.Require(currentUser, Admin);
        var app = await appRepo.GetByIdAsync(appId)
            ?? throw new InvalidOperationException("App not found");
        app.CiToken = null;
        await appRepo.UpdateAsync(app);
        await audit.LogAsync("ClearAppCiToken", entityType: "App", entityId: appId.ToString(), details: app.Slug);
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
