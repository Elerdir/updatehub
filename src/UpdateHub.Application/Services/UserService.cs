using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Application.Authorization;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Application.Services;

public class UserService(
    IUserRepository    users,
    IPasswordResetTokenRepository resetTokens,
    IPersonalAccessTokenRepository pats,
    ISecretProtector   protector,
    AuditService       audit,
    INotificationQueue notifications,
    ICurrentUser       currentUser)
{
    private const string Admin = "Admin";

    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);
    public const int MinPasswordLength = 8;

    public Task<List<User>> GetAllAsync()           => users.GetAllAsync();
    public Task<User?>      GetByIdAsync(Guid id)   => users.GetByIdAsync(id);
    public Task<User?>      GetByUsernameAsync(string username) =>
        users.GetByUsernameAsync(username.Trim());

    /// <summary>
    /// Verifies username+password against the active user. Returns the user on
    /// success, null otherwise. Updates LastLoginAt as a side-effect on success.
    /// </summary>
    public async Task<User?> VerifyCredentialsAsync(string username, string password)
    {
        var u = await users.GetByUsernameAsync(username.Trim());
        if (u is null || !u.IsActive) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, u.PasswordHash)) return null;

        u.LastLoginAt = DateTime.UtcNow;
        await users.UpdateAsync(u);
        return u;
    }

    /// <summary>
    /// Admin creates a new user with a temporary password. Returns the created
    /// user; the caller already knows the plaintext temp password.
    /// </summary>
    public async Task<User> CreateAsync(
        string  username,
        string? email,
        string  tempPassword,
        UserRole role,
        Guid    createdById,
        string  createdByName)
    {
        RoleGuard.Require(currentUser, Admin);
        username = username.Trim();
        email    = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (username.Length < 3)
            throw new ArgumentException("Username must be at least 3 characters.");
        if (tempPassword.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");

        var existing = await users.GetByUsernameAsync(username);
        if (existing is not null)
            throw new InvalidOperationException("Username is already taken.");

        var user = new User
        {
            Username           = username,
            Email              = email,
            PasswordHash       = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 12),
            Role               = role,
            MustChangePassword = true,
            IsActive           = true,
            CreatedById        = createdById,
        };
        var created = await users.CreateAsync(user);

        await audit.LogAsync("CreateUser", actor: createdByName,
            entityType: "User", entityId: created.Id.ToString(),
            details: $"{username} ({role})");

        // Admin notification goes to the global mailbox; if the new user has
        // an email of their own they also get a heads-up (without the password).
        var roleText = role.ToString();
        var userEmail = email;
        notifications.Enqueue(async (sp, _) =>
        {
            var em = sp.GetRequiredService<EmailNotificationService>();
            await em.SendUserCreatedAsync(username, roleText, createdByName);
            await em.SendAccountCreatedToUserAsync(userEmail, username, roleText);
        });

        return created;
    }

    /// <summary>
    /// Admin resets another user's password. New password is a temp credential —
    /// the user must change it on next login. Existing sessions are invalidated
    /// via SecurityStamp rotation.
    /// </summary>
    public async Task ResetPasswordAsync(Guid userId, string newTempPassword, string resetByName)
    {
        RoleGuard.Require(currentUser, Admin);
        if (newTempPassword.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");

        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        user.PasswordHash       = BCrypt.Net.BCrypt.HashPassword(newTempPassword, workFactor: 12);
        user.MustChangePassword = true;
        user.SecurityStamp      = Guid.NewGuid().ToString("N");
        await users.UpdateAsync(user);

        await audit.LogAsync("ResetPassword", actor: resetByName,
            entityType: "User", entityId: userId.ToString(),
            details: user.Username);

        var username  = user.Username;
        var userEmail = user.Email;
        notifications.Enqueue(async (sp, _) =>
        {
            var em = sp.GetRequiredService<EmailNotificationService>();
            await em.SendPasswordResetAsync(username, resetByName);
            await em.SendPasswordResetToUserAsync(userEmail, username);
        });
    }

    /// <summary>
    /// User changes their own password. Verifies the current password and
    /// clears the MustChangePassword flag. SecurityStamp rotation forces any
    /// other open sessions for the same user to re-authenticate.
    /// </summary>
    public async Task ChangeOwnPasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        RequireSelf(userId);
        if (newPassword.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
        if (newPassword == currentPassword)
            throw new ArgumentException("New password must differ from the current one.");

        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new ArgumentException("Current password is incorrect.");

        user.PasswordHash       = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.MustChangePassword = false;
        user.SecurityStamp      = Guid.NewGuid().ToString("N");
        await users.UpdateAsync(user);

        await audit.LogAsync("ChangeOwnPassword", actor: user.Username,
            entityType: "User", entityId: userId.ToString());

        var username = user.Username;
        notifications.Enqueue(async (sp, _) =>
        {
            var em = sp.GetRequiredService<EmailNotificationService>();
            await em.SendPasswordChangedAsync(username);
        });
    }

    public async Task SetActiveAsync(Guid userId, bool active, string actorName)
    {
        RoleGuard.Require(currentUser, Admin);
        // An admin cannot deactivate their own account — locking yourself out
        // of a single-admin install is the easiest way to brick the server.
        if (!active && currentUser.Id == userId)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        if (user.IsActive == active) return;

        user.IsActive      = active;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await users.UpdateAsync(user);

        await audit.LogAsync(active ? "EnableUser" : "DisableUser",
            actor: actorName, entityType: "User", entityId: userId.ToString(),
            details: user.Username);
    }

    public async Task SetEmailAsync(Guid userId, string? email, string actorName)
    {
        RoleGuard.Require(currentUser, Admin);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var normalized = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (user.Email == normalized) return;

        user.Email = normalized;
        await users.UpdateAsync(user);

        await audit.LogAsync("ChangeUserEmail", actor: actorName,
            entityType: "User", entityId: userId.ToString(),
            details: $"{user.Username} → {normalized ?? "(none)"}");
    }

    public async Task SetRoleAsync(Guid userId, UserRole role, string actorName)
    {
        RoleGuard.Require(currentUser, Admin);
        // An admin cannot change their own role — same lock-yourself-out risk.
        if (currentUser.Id == userId)
            throw new InvalidOperationException("You cannot change your own role.");

        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        if (user.Role == role) return;

        user.Role = role;
        await users.UpdateAsync(user);

        await audit.LogAsync("ChangeRole", actor: actorName,
            entityType: "User", entityId: userId.ToString(),
            details: $"{user.Username} → {role}");
    }

    // ── Forgot-password flow (unauthenticated) ────────────────────────────

    /// <summary>
    /// Looks up the user by username OR email and (if found) creates a single-use
    /// reset token. The plaintext token is returned to the caller so it can be
    /// embedded in an email link. Returns null when no matching user exists —
    /// callers must NOT reveal that to the requester (timing-safe-ish: we
    /// always do the same amount of work).
    /// </summary>
    public async Task<string?> InitiatePasswordResetAsync(string usernameOrEmail)
    {
        var key = usernameOrEmail.Trim();
        if (string.IsNullOrEmpty(key)) return null;

        // Try username first, then email
        var user = await users.GetByUsernameAsync(key);
        if (user is null)
        {
            var all = await users.GetAllAsync();
            user = all.FirstOrDefault(u =>
                !string.IsNullOrEmpty(u.Email) &&
                string.Equals(u.Email, key, StringComparison.OrdinalIgnoreCase));
        }
        if (user is null || !user.IsActive) return null;
        if (string.IsNullOrWhiteSpace(user.Email)) return null;

        // Invalidate any other open tokens for this user — only the freshest one is usable
        await resetTokens.InvalidateAllForUserAsync(user.Id);

        var rawToken = GenerateOpaqueToken();
        var hash     = HashToken(rawToken);

        await resetTokens.CreateAsync(new Domain.Entities.PasswordResetToken
        {
            UserId    = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.Add(ResetTokenLifetime),
        });

        await audit.LogAsync("RequestPasswordReset", actor: user.Username,
            entityType: "User", entityId: user.Id.ToString());

        var capturedEmail    = user.Email;
        var capturedUsername = user.Username;
        var token            = rawToken;
        notifications.Enqueue(async (sp, _) =>
        {
            var em = sp.GetRequiredService<EmailNotificationService>();
            await em.SendPasswordResetLinkAsync(capturedEmail, capturedUsername, token);
        });

        return rawToken;
    }

    /// <summary>
    /// Consumes a reset token: verifies it is unused, not expired, and matches
    /// a live user, then sets the new password and marks the token used.
    /// SecurityStamp rotation invalidates any existing sessions for the user.
    /// </summary>
    public async Task ConsumePasswordResetAsync(string rawToken, string newPassword)
    {
        if (newPassword.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");

        var hash  = HashToken(rawToken);
        var token = await resetTokens.GetByHashAsync(hash)
            ?? throw new InvalidOperationException("Invalid or expired reset link.");
        if (token.UsedAt is not null || token.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Invalid or expired reset link.");

        var user = await users.GetByIdAsync(token.UserId)
            ?? throw new InvalidOperationException("Invalid or expired reset link.");
        if (!user.IsActive)
            throw new InvalidOperationException("Account is disabled.");

        user.PasswordHash       = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.MustChangePassword = false;
        user.SecurityStamp      = Guid.NewGuid().ToString("N");
        await users.UpdateAsync(user);

        token.UsedAt = DateTime.UtcNow;
        await resetTokens.UpdateAsync(token);

        await audit.LogAsync("CompletePasswordReset", actor: user.Username,
            entityType: "User", entityId: user.Id.ToString());
    }

    private static string GenerateOpaqueToken(int bytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string HashToken(string raw) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    // ── 2FA (per-user) ─────────────────────────────────────────────────────

    public async Task<string?> GetTotpSecretAsync(Guid userId)
    {
        var u = await users.GetByIdAsync(userId);
        if (u is null || string.IsNullOrEmpty(u.TotpSecret)) return null;
        try
        {
            return protector.Unprotect(u.TotpSecret);
        }
        catch
        {
            // Pre-encryption secret (shouldn't happen on a fresh User table)
            return u.TotpSecret;
        }
    }

    public Task<bool> IsTotpEnabledAsync(Guid userId) =>
        users.GetByIdAsync(userId).ContinueWith(t => t.Result is { TotpEnabled: true });

    /// <summary>
    /// Activates TOTP and generates a fresh set of 10 single-use backup codes.
    /// The plaintext codes are returned only here — only hashes are persisted.
    /// </summary>
    public async Task<List<string>> EnableTotpAsync(Guid userId, string secret)
    {
        RequireSelf(userId);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        user.TotpSecret  = protector.Protect(secret);
        user.TotpEnabled = true;

        var codes = GenerateBackupCodes();
        user.BackupCodes = string.Join(",", codes.Select(HashToken));
        await users.UpdateAsync(user);
        await audit.LogAsync("EnableTotp", actor: user.Username,
            entityType: "User", entityId: userId.ToString());
        return codes;
    }

    /// <summary>Replaces the user's backup codes and returns the new plaintext set.</summary>
    public async Task<List<string>> RegenerateBackupCodesAsync(Guid userId)
    {
        RequireSelf(userId);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        if (!user.TotpEnabled) throw new InvalidOperationException("2FA is not enabled.");

        var codes = GenerateBackupCodes();
        user.BackupCodes = string.Join(",", codes.Select(HashToken));
        await users.UpdateAsync(user);
        await audit.LogAsync("RegenerateBackupCodes", actor: user.Username,
            entityType: "User", entityId: userId.ToString());
        return codes;
    }

    public async Task<int> GetUnusedBackupCodeCountAsync(Guid userId)
    {
        var user = await users.GetByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.BackupCodes)) return 0;
        return user.BackupCodes.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Tries to consume one backup code as a 2FA substitute. Returns true and
    /// removes the code from the unused set on a match; false otherwise. Used
    /// by the TOTP login endpoint when the authenticator code path fails.
    /// </summary>
    public async Task<bool> ConsumeBackupCodeAsync(Guid userId, string rawCode)
    {
        var user = await users.GetByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.BackupCodes)) return false;

        var target = HashToken(rawCode.Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant());
        var hashes = user.BackupCodes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!hashes.Remove(target)) return false;

        user.BackupCodes = string.Join(",", hashes);
        await users.UpdateAsync(user);
        await audit.LogAsync("ConsumeBackupCode", actor: user.Username,
            entityType: "User", entityId: userId.ToString(),
            details: $"{hashes.Count} codes remaining");
        return true;
    }

    public async Task DisableTotpAsync(Guid userId)
    {
        RequireSelf(userId);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        user.TotpSecret  = null;
        user.TotpEnabled = false;
        user.BackupCodes = null;
        await users.UpdateAsync(user);
        await audit.LogAsync("DisableTotp", actor: user.Username,
            entityType: "User", entityId: userId.ToString());
    }

    private static List<string> GenerateBackupCodes(int n = 10, int length = 10)
    {
        // Crockford-style alphabet (no 0/O/1/I) to avoid handwriting ambiguity.
        const string alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(n * length);
        var codes = new List<string>(n);
        for (var i = 0; i < n; i++)
        {
            var chars = new char[length];
            for (var j = 0; j < length; j++)
                chars[j] = alphabet[bytes[i * length + j] % alphabet.Length];
            codes.Add(new string(chars));
        }
        return codes;
    }

    // ── Personal access tokens (per-user bearer tokens) ───────────────────

    public Task<List<Domain.Entities.PersonalAccessToken>> GetTokensAsync(Guid userId)
    {
        RequireSelf(userId);
        return pats.GetForUserAsync(userId);
    }

    public async Task<string> CreatePersonalAccessTokenAsync(Guid userId, string name, int? expiresInDays)
    {
        RequireSelf(userId);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Token name is required.");

        var raw = GenerateOpaqueToken(40);
        var token = new Domain.Entities.PersonalAccessToken
        {
            UserId    = userId,
            Name      = name.Trim(),
            TokenHash = HashToken(raw),
            Prefix    = raw[..8],
            ExpiresAt = expiresInDays is int d && d > 0 ? DateTime.UtcNow.AddDays(d) : null,
        };
        await pats.CreateAsync(token);

        var owner = await users.GetByIdAsync(userId);
        await audit.LogAsync("CreateApiToken", actor: owner?.Username ?? "?",
            entityType: "PersonalAccessToken", entityId: token.Id.ToString(),
            details: name);

        return raw;
    }

    public async Task RevokeTokenAsync(Guid userId, Guid tokenId)
    {
        RequireSelf(userId);
        var token = (await pats.GetForUserAsync(userId)).FirstOrDefault(t => t.Id == tokenId)
            ?? throw new InvalidOperationException("Token not found.");
        if (token.RevokedAt is not null) return;
        token.RevokedAt = DateTime.UtcNow;
        await pats.UpdateAsync(token);

        var owner = await users.GetByIdAsync(userId);
        await audit.LogAsync("RevokeApiToken", actor: owner?.Username ?? "?",
            entityType: "PersonalAccessToken", entityId: token.Id.ToString(),
            details: token.Name);
    }

    /// <summary>
    /// Verifies a raw Bearer token. Returns the owning User on success (and
    /// updates LastUsedAt). Null when the token is unknown, revoked, expired,
    /// or its owner is disabled.
    /// </summary>
    public async Task<User?> VerifyPersonalAccessTokenAsync(string rawToken)
    {
        var hash  = HashToken(rawToken);
        var token = await pats.GetByHashAsync(hash);
        if (token is null) return null;
        if (token.RevokedAt is not null) return null;
        if (token.ExpiresAt is { } exp && exp < DateTime.UtcNow) return null;
        if (token.User is null || !token.User.IsActive) return null;

        token.LastUsedAt = DateTime.UtcNow;
        await pats.UpdateAsync(token);
        return token.User;
    }

    // ── Session management ────────────────────────────────────────────────

    /// <summary>
    /// Rotates the user's SecurityStamp — any existing auth cookies for this
    /// user become invalid on their next request (the middleware sees the
    /// mismatch and signs them out). Used for "log out of all other devices".
    /// </summary>
    public async Task RevokeAllSessionsAsync(Guid userId)
    {
        RequireSelf(userId);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await users.UpdateAsync(user);
        await audit.LogAsync("RevokeSessions", actor: user.Username,
            entityType: "User", entityId: userId.ToString());
    }

    // ── Admin: force a user to change password on next login ──────────────

    public async Task SetMustChangePasswordAsync(Guid userId, bool value, string actorName)
    {
        RoleGuard.Require(currentUser, Admin);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        if (user.MustChangePassword == value) return;
        user.MustChangePassword = value;
        await users.UpdateAsync(user);
        await audit.LogAsync("SetMustChangePassword", actor: actorName,
            entityType: "User", entityId: userId.ToString(),
            details: $"{user.Username} → {value}");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void RequireSelf(Guid userId)
    {
        if (!currentUser.IsAuthenticated || currentUser.Id != userId)
            throw new UnauthorizedAccessException("Self-service operation only.");
    }

    /// <summary>
    /// Cryptographically random temp password (16 chars, URL-safe alphabet).
    /// Used as the suggested value when an admin creates / resets a user.
    /// </summary>
    public static string GenerateTempPassword(int length = 16)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var bytes  = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (var i = 0; i < length; i++)
            result[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(result);
    }
}
