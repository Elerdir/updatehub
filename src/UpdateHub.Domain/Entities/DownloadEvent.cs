namespace UpdateHub.Domain.Entities;

/// <summary>
/// One row per artifact download. Used by the analytics page to chart adoption
/// curves and break downloads down by platform / arch. IP is hashed (not raw)
/// so we can count unique clients without storing personally identifiable
/// addresses.
/// </summary>
public class DownloadEvent
{
    public long      Id           { get; set; }
    public Guid      ArtifactId   { get; set; }
    public Artifact  Artifact     { get; set; } = null!;
    public Guid      AppId        { get; set; }
    public string    AppSlug      { get; set; } = "";
    public string    Version      { get; set; } = "";
    public string    Platform     { get; set; } = "";
    public string    Architecture { get; set; } = "";
    public DateTime  At           { get; set; } = DateTime.UtcNow;
    public string?   UserAgent    { get; set; }
    public string?   IpHash       { get; set; }
}
