namespace UpdateHub.Application.Models;

public record TauriManifest(
    string Version,
    string Notes,
    string PubDate,
    Dictionary<string, TauriPlatformEntry> Platforms);

public record TauriPlatformEntry(string Signature, string Url);
