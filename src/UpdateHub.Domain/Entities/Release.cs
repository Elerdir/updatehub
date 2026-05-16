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

    /// <summary>
    /// Optional lowest installed version that may upgrade DIRECTLY to this one.
    /// When set and the client is below this threshold, the update resolver
    /// returns the highest available stepping-stone release instead. Used to
    /// enforce sequential upgrades when a release contains a one-way
    /// migration (DB schema, config format, encryption keys, etc.).
    /// Null = anyone can upgrade straight to this version.
    /// </summary>
    public string? MinFromVersion { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Artifact> Artifacts { get; set; } = [];
}
