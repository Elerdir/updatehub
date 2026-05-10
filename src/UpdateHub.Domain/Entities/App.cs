namespace UpdateHub.Domain.Entities;

public class App
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CiToken { get; set; }
    public List<Release> Releases { get; set; } = [];
}
