namespace UpdateHub.Web.Data.Entities;

public class Artifact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReleaseId { get; set; }
    public Release Release { get; set; } = null!;
    public string Platform { get; set; } = "";      // windows, macos, linux
    public string Architecture { get; set; } = "";  // x64, arm64, x86
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string? Signature { get; set; }          // Tauri Ed25519 signature
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
