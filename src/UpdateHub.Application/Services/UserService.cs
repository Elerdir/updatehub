using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Application.Authorization;
using UpdateHub.Application.Interfaces;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Application.Services;

public class UserService(
    IUserRepository    users,
    ISecretProtector   protector,
    AuditService       audit,
    INotificationQueue notifications,
    ICurrentUser       currentUser)
{
    private const string Admin = "Admin";
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
        string username, string tempPassword, UserRole role, Guid createdById, string createdByName)
    {
        RoleGuard.Require(currentUser, Admin);
        username = username.Trim();
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

        notifications.Enqueue(async (sp, _) =>
        {
            var em = sp.GetRequiredService<EmailNotificationService>();
            await em.SendUserCreatedAsync(username, role.ToString(), createdByName);
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

        var username = user.Username;
        notifications.Enqueue(async (sp, _) =>
        {
            var em = sp.GetRequiredService<EmailNotificationService>();
            await em.SendPasswordResetAsync(username, resetByName);
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

    public async Task SetRoleAsync(Guid userId, UserRole role, string actorName)
    {
        RoleGuard.Require(currentUser, Admin);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        if (user.Role == role) return;

        user.Role = role;
        await users.UpdateAsync(user);

        await audit.LogAsync("ChangeRole", actor: actorName,
            entityType: "User", entityId: userId.ToString(),
            details: $"{user.Username} → {role}");
    }

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

    public async Task EnableTotpAsync(Guid userId, string secret)
    {
        RequireSelf(userId);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        user.TotpSecret  = protector.Protect(secret);
        user.TotpEnabled = true;
        await users.UpdateAsync(user);
        await audit.LogAsync("EnableTotp", actor: user.Username,
            entityType: "User", entityId: userId.ToString());
    }

    public async Task DisableTotpAsync(Guid userId)
    {
        RequireSelf(userId);
        var user = await users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        user.TotpSecret  = null;
        user.TotpEnabled = false;
        await users.UpdateAsync(user);
        await audit.LogAsync("DisableTotp", actor: user.Username,
            entityType: "User", entityId: userId.ToString());
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
