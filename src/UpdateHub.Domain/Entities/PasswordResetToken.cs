namespace UpdateHub.Domain.Entities;

public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user this token can reset.</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the raw token — only the user (and the email they received) holds the plaintext.</summary>
    public string TokenHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set once the token has been consumed. Tokens are single-use.</summary>
    public DateTime? UsedAt { get; set; }
}
