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
        // Pull every published release for this channel so we can pick the
        // best one the client may install DIRECTLY — taking MinFromVersion
        // constraints into account. Older clients are stepped up to the
        // highest release whose MinFromVersion they satisfy; on the next
        // poll they will be offered the version after that, and so on.
        var candidates = await releases.GetAllPublishedAsync(appSlug, ParseChannel(channel));
        if (candidates is null || candidates.Count == 0) return null;

        var release = PickBestForVersion(candidates, currentVersion);
        if (release is null)
        {
            // No newer release exists — pretend the latest *known* release
            // so the client sees has_update=false (otherwise we'd return 404
            // and break older clients that rely on a non-null body).
            release = candidates[0];
        }

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

    /// <summary>
    /// Among the published releases newer than <paramref name="currentVersion"/>,
    /// returns the highest one the client can install directly (i.e. whose
    /// <see cref="Release.MinFromVersion"/> is null or ≤ currentVersion).
    /// If none of the eligible releases is newer than the current, returns null.
    /// Releases are compared by SemVer precedence so a hotfix released after a
    /// major doesn't accidentally become "latest" via publish date.
    /// </summary>
    public static Release? PickBestForVersion(IList<Release> published, string currentVersion)
    {
        // Sort newest-version-first so the first match is the most advanced
        // step the client may take.
        var orderedByVersion = published
            .OrderByDescending(r => r.Version, SemverComparer.Instance)
            .ToList();

        foreach (var r in orderedByVersion)
        {
            if (!IsNewer(currentVersion, r.Version)) continue;
            if (MeetsMinimum(currentVersion, r.MinFromVersion))
                return r;
        }
        return null;
    }

    /// <summary>currentVersion ≥ minVersion in SemVer precedence, with sensible fallbacks.</summary>
    public static bool MeetsMinimum(string currentVersion, string? minVersion)
    {
        if (string.IsNullOrWhiteSpace(minVersion)) return true;
        var c = currentVersion.TrimStart('v', 'V');
        var m = minVersion.TrimStart('v', 'V');
        if (SemVersion.TryParse(c, SemVersionStyles.Any, out var cs) &&
            SemVersion.TryParse(m, SemVersionStyles.Any, out var ms))
            return cs.ComparePrecedenceTo(ms) >= 0;
        if (Version.TryParse(c, out var cv) && Version.TryParse(m, out var mv))
            return cv >= mv;
        return string.Compare(c, m, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private sealed class SemverComparer : IComparer<string>
    {
        public static readonly SemverComparer Instance = new();
        public int Compare(string? x, string? y)
        {
            var a = (x ?? "").TrimStart('v', 'V');
            var b = (y ?? "").TrimStart('v', 'V');
            if (SemVersion.TryParse(a, SemVersionStyles.Any, out var av) &&
                SemVersion.TryParse(b, SemVersionStyles.Any, out var bv))
                return av.ComparePrecedenceTo(bv);
            if (Version.TryParse(a, out var ax) && Version.TryParse(b, out var bx))
                return ax.CompareTo(bx);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }
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
