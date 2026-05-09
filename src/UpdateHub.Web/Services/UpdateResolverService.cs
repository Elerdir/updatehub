using Microsoft.EntityFrameworkCore;
using UpdateHub.Web.Data;
using UpdateHub.Web.Data.Entities;

namespace UpdateHub.Web.Services;

public class UpdateResolverService(AppDbContext db, IConfiguration config)
{
    private string BaseUrl => config["UpdateHub:BaseUrl"]?.TrimEnd('/') ?? "";

    public async Task<TauriManifest?> GetTauriManifestAsync(string appSlug, string? channel)
    {
        var ch = ParseChannel(channel);

        var release = await db.Releases
            .Include(r => r.Artifacts)
            .Include(r => r.App)
            .Where(r => r.App.Slug == appSlug
                     && r.Status == ReleaseStatus.Published
                     && r.Channel == ch)
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync();

        if (release is null) return null;

        var platforms = new Dictionary<string, TauriPlatformEntry>();
        foreach (var a in release.Artifacts)
        {
            var key = TauriPlatformKey(a.Platform, a.Architecture);
            if (key is not null && a.Signature is not null)
                platforms[key] = new TauriPlatformEntry(a.Signature, $"{BaseUrl}/api/downloads/{a.Id}");
        }

        return new TauriManifest(
            release.Version,
            release.ReleaseNotes ?? "",
            release.PublishedAt?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
            platforms);
    }

    public async Task<UpdateCheckResult?> CheckUpdateAsync(
        string appSlug, string currentVersion, string? platform, string? arch, string? channel)
    {
        var ch = ParseChannel(channel);

        var release = await db.Releases
            .Include(r => r.Artifacts)
            .Include(r => r.App)
            .Where(r => r.App.Slug == appSlug
                     && r.Status == ReleaseStatus.Published
                     && r.Channel == ch)
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync();

        if (release is null) return null;

        var hasUpdate = IsNewer(currentVersion, release.Version);

        Artifact? artifact = null;
        if (hasUpdate && platform is not null)
            artifact = release.Artifacts.FirstOrDefault(a =>
                a.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                (arch is null || a.Architecture.Equals(arch, StringComparison.OrdinalIgnoreCase)));

        return new UpdateCheckResult(
            hasUpdate,
            release.Version,
            release.ReleaseNotes,
            artifact is not null ? $"{BaseUrl}/api/downloads/{artifact.Id}" : null,
            artifact?.Sha256,
            release.IsMandatory,
            release.Channel.ToString().ToLower());
    }

    private static ReleaseChannel ParseChannel(string? ch) => ch?.ToLower() switch
    {
        "beta"  => ReleaseChannel.Beta,
        "alpha" => ReleaseChannel.Alpha,
        _       => ReleaseChannel.Stable
    };

    private static string? TauriPlatformKey(string platform, string arch) =>
        (platform.ToLower(), arch.ToLower()) switch
        {
            ("windows", "x64")   => "windows-x86_64",
            ("windows", "x86")   => "windows-i686",
            ("macos",   "x64")   => "darwin-x86_64",
            ("macos",   "arm64") => "darwin-aarch64",
            ("linux",   "x64")   => "linux-x86_64",
            ("linux",   "arm64") => "linux-aarch64",
            _ => null
        };

    private static bool IsNewer(string current, string latest)
    {
        if (Version.TryParse(current.TrimStart('v'), out var c) &&
            Version.TryParse(latest.TrimStart('v'), out var l))
            return l > c;
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }
}

public record TauriManifest(
    string Version, string Notes, string PubDate,
    Dictionary<string, TauriPlatformEntry> Platforms);

public record TauriPlatformEntry(string Signature, string Url);

public record UpdateCheckResult(
    bool HasUpdate, string Version, string? ReleaseNotes,
    string? DownloadUrl, string? Sha256, bool IsMandatory, string Channel);
