using UpdateHub.Domain.Enums;

namespace UpdateHub.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Login name. Immutable once created.</summary>
    public string Username { get; set; } = "";

    /// <summary>bcrypt hash of the current password.</summary>
    public string PasswordHash { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.Viewer;

    /// <summary>
    /// True when the user must change their password on next login (after
    /// initial creation or an admin-driven reset). Set to false once the user
    /// completes the change-password flow.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Soft-disable flag — disabled users cannot log in but their entries
    /// (audit log, created releases, etc.) stay attributed.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Random token that is rotated on every password change / reset. Embedded
    /// in the auth cookie so existing sessions are invalidated when the
    /// password changes.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Encrypted (via ISecretProtector) base32 TOTP secret, or null if 2FA off.</summary>
    public string? TotpSecret { get; set; }

    public bool TotpEnabled { get; set; }

    /// <summary>
    /// Comma-separated SHA-256 hex hashes of UNUSED single-use 2FA backup codes.
    /// Codes themselves are high-entropy (10 char alphanumeric ≈ 60 bits), so an
    /// unsalted hash is safe — a rainbow table is infeasible at that size.
    /// </summary>
    public string? BackupCodes { get; set; }

    /// <summary>
    /// Optional contact email for account-event notifications (account created,
    /// password reset). When null the user is not notified — admin still gets
    /// the global notification at <c>Smtp:To</c>. We never send the actual
    /// password through this channel; that goes out-of-band.
    /// </summary>
    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Audit trail — which admin created this account (null for the bootstrap admin).</summary>
    public Guid? CreatedById { get; set; }

    public List<PersonalAccessToken> Tokens { get; set; } = [];
}
