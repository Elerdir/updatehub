using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Models;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Application.Services;

public class UpdateResolverService(
    IReleaseRepository releases,
    IArtifactRepository artifacts,
    string baseUrl)
{
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
            artifact = release.Artifacts.FirstOrDefault(a =>
                a.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                (arch is null || a.Architecture.Equals(arch, StringComparison.OrdinalIgnoreCase)));

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
        if (Version.TryParse(current.TrimStart('v'), out var c) &&
            Version.TryParse(latest.TrimStart('v'), out var l))
            return l > c;
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
