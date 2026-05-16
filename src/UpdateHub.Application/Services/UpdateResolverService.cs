using Semver;
using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Models;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Application.Services;

public class UpdateResolverService(
    IReleaseRepository releases,
    string baseUrl)
{
    /// <summary>
    /// Returns the latest published release for a given app + channel together
    /// with a fully resolved download URL for the best-matching artifact, or
    /// null when nothing is published. Used by the format-specific endpoints
    /// (electron-updater YAML, Sparkle XML, Velopack JSON) that all need the
    /// same data and differ only in the wire format.
    /// </summary>
    public async Task<(Release Release, Artifact Artifact, string Url)?>
        GetLatestForFormatAsync(string appSlug, string platform, string arch, string? channel)
    {
        var release = await releases.GetLatestPublishedAsync(appSlug, ParseChannel(channel));
        if (release is null) return null;

        var artifact = release.Artifacts
            .Where(a => a.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)
                     && a.Architecture.Equals(arch, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (artifact is null) return null;
        return (release, artifact, DownloadUrl(artifact.Id));
    }

    public async Task<TauriManifest?> GetTauriManifestAsync(string appSlug, string? channel)
    {
        var release = await releases.GetLatestPublishedAsync(appSlug, ParseChannel(channel));
        if (release is null) return null;

        var platforms = new Dictionary<string, TauriPlatformEntry>();
        foreach (var a in release.Artifacts)
        {
            var key = TauriPlatformKey(a.Platform, a.Architecture);
            if (key is not null && a.Signature is not null)
                platforms[key] = new TauriPlatformEntry(a.Signature, DownloadUrl(a.Id));
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
        var release = await releases.GetLatestPublishedAsync(appSlug, ParseChannel(channel));
        if (release is null) return null;

        var hasUpdate = IsNewer(currentVersion, release.Version);

        Artifact? artifact = null;
        if (hasUpdate && platform is not null)
        {
            // If the publisher uploaded multiple files for the same platform/arch
            // (e.g. setup.exe + portable.zip), the most recently uploaded one is
            // treated as authoritative for the update endpoint.
            artifact = release.Artifacts
                .Where(a => a.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)
                         && (arch is null || a.Architecture.Equals(arch, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();
        }

        return new UpdateCheckResult(
            hasUpdate,
            release.Version,
            release.ReleaseNotes,
            artifact is not null ? DownloadUrl(artifact.Id) : null,
            artifact?.Sha256,
            release.IsMandatory,
            release.Channel.ToString().ToLower());
    }

    private string DownloadUrl(Guid artifactId) =>
        $"{baseUrl.TrimEnd('/')}/api/downloads/{artifactId}";

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
        var currentTrim = current.TrimStart('v', 'V');
        var latestTrim  = latest.TrimStart('v', 'V');

        // Prefer full semver comparison — correctly handles pre-release tags
        // (1.0.0-beta < 1.0.0) and build metadata.
        if (SemVersion.TryParse(currentTrim, SemVersionStyles.Any, out var c) &&
            SemVersion.TryParse(latestTrim, SemVersionStyles.Any, out var l))
            return l.ComparePrecedenceTo(c) > 0;

        // Fall back to System.Version for plain numeric versions, then to
        // an ordinal string compare as a last resort.
        if (Version.TryParse(currentTrim, out var cv) &&
            Version.TryParse(latestTrim, out var lv))
            return lv > cv;

        return string.Compare(latestTrim, currentTrim, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
