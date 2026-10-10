namespace UpdateHub.Domain.Entities;

/// <summary>
/// Long-lived Bearer token bound to a single user. Useful for scripted
/// uploads / CLI workflows when you want auditability per-user instead of
/// sharing a global CI token.
/// </summary>
public class PersonalAccessToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Human label so the user can tell their tokens apart.</summary>
    public string Name { get; set; } = "";

    /// <summary>SHA-256 hash of the raw token. Plaintext exists only at creation time.</summary>
    public string TokenHash { get; set; } = "";

    /// <summary>First 8 chars of the raw token, for visual identification in the list.</summary>
    public string Prefix { get; set; } = "";

    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt  { get; set; }
    public DateTime? RevokedAt  { get; set; }
}
