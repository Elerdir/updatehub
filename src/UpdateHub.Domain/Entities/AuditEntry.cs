namespace UpdateHub.Domain.Entities;

public class AuditEntry
{
    public Guid     Id         { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string   Actor      { get; set; } = "system";
    public string   Action     { get; set; } = "";
    public string?  EntityType { get; set; }
    public string?  EntityId   { get; set; }
    public string?  Details    { get; set; }
    public string?  IpAddress  { get; set; }
}
