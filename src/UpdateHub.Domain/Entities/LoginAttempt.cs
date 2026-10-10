namespace UpdateHub.Domain.Entities;

public class LoginAttempt
{
    public string IpAddress { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTime FirstAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAttemptAt { get; set; } = DateTime.UtcNow;
    public string? LastUserAgent { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsManualBlock { get; set; }   // admin-initiated vs auto (threshold)
    public DateTime? BlockedAt { get; set; }
    public DateTime? UnblockedAt { get; set; } // last time admin manually unblocked
}
