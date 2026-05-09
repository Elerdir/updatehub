using UpdateHub.Domain.Enums;

namespace UpdateHub.Domain.Entities;

public class Release
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppId { get; set; }
    public App App { get; set; } = null!;
    public string Version { get; set; } = "";
    public ReleaseChannel Channel { get; set; } = ReleaseChannel.Stable;
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Draft;
    public string? ReleaseNotes { get; set; }
    public bool IsMandatory { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Artifact> Artifacts { get; set; } = [];
}
