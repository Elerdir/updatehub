using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Services;
using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;
using UpdateHub.Infrastructure.Persistence;

namespace UpdateHub.Web.Startup;

public static class BootstrapSeeder
{
    /// <summary>
    /// On first run (Users table empty) creates the bootstrap admin from
    /// UpdateHub:Admin:* config so the operator can log in. Also migrates any
    /// legacy 2FA secret stored under the old AppSetting keys to the new admin
    /// User. Subsequent runs are a no-op.
    /// </summary>
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var sp        = scope.ServiceProvider;
        var config    = sp.GetRequiredService<IConfiguration>();
        var users     = sp.GetRequiredService<IUserRepository>();
        var settings  = sp.GetRequiredService<SettingsService>();
        var rawSettings = sp.GetRequiredService<ISettingsRepository>();
        var db        = sp.GetRequiredService<AppDbContext>();
        var log       = sp.GetRequiredService<ILogger<AppDbContext>>();

        if (await users.CountAsync() > 0) return;

        var username = config["UpdateHub:Admin:Username"] ?? "admin";

        // Effective password hash: prefer one already stored in Settings (set
        // via the old Settings page change-password flow), fall back to config.
        var dbHash    = await settings.GetAdminPasswordHashAsync();
        var hash      = string.IsNullOrWhiteSpace(dbHash)
            ? (config["UpdateHub:Admin:PasswordHash"] ?? "")
            : dbHash;

        if (string.IsNullOrWhiteSpace(hash))
        {
            log.LogWarning("No bootstrap admin password configured — cannot seed admin user. " +
                "Set UpdateHub:Admin:PasswordHash and restart.");
            return;
        }

        var admin = new User
        {
            Username           = username,
            PasswordHash       = hash,
            Role               = UserRole.Admin,
            MustChangePassword = false,
            IsActive           = true,
        };

        // Carry over an existing 2FA secret/enabled flag, if any
        var legacySecretEnc = await rawSettings.GetAsync("Totp:Secret");
        var legacyEnabled   = await rawSettings.GetAsync("Totp:Enabled") == "true";
        if (!string.IsNullOrEmpty(legacySecretEnc))
        {
            admin.TotpSecret  = legacySecretEnc; // already protected — stays as-is
            admin.TotpEnabled = legacyEnabled;
        }

        db.Users.Add(admin);
        await db.SaveChangesAsync();

        log.LogInformation("Bootstrap admin '{Username}' seeded.", username);
    }
}
