namespace UpdateHub.Application.Models;

public record UpdateCheckResult(
    bool HasUpdate,
    string Version,
    string? ReleaseNotes,
    string? DownloadUrl,
    string? Sha256,
    bool IsMandatory,
    string Channel);
