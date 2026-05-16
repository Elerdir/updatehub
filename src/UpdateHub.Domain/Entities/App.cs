namespace UpdateHub.Domain.Entities;

public class App
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CiToken { get; set; }

    /// <summary>
    /// Optional per-app webhook URL — when set, publish events for this app
    /// POST here instead of the global UpdateHub:WebhookUrl. Useful when
    /// different apps notify different chat channels.
    /// </summary>
    public string? WebhookUrl { get; set; }

    public List<Release> Releases { get; set; } = [];
}
