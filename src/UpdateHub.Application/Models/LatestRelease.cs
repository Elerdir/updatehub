namespace UpdateHub.Application.Models;

/// <summary>
/// The latest published release with every artifact resolved to a download
/// URL — for download pages (e.g. a personal website), not for updaters.
/// </summary>
public record LatestRelease(
    string Slug,
    string Name,
    string Version,
    string Channel,
    DateTime? PublishedAt,
    string? ReleaseNotes,
    IReadOnlyList<LatestArtifact> Artifacts);

public record LatestArtifact(
    string Platform,
    string Architecture,
    string FileName,
    long SizeBytes,
    string Sha256,
    string Url);
