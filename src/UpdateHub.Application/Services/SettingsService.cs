using UpdateHub.Application.Interfaces;
using UpdateHub.Application.Models;

namespace UpdateHub.Application.Services;

public class SettingsService(ISettingsRepository repo, ISecretProtector protector)
{
    private const string CiTokenKey        = "CiToken";
    private const string AdminPasswordKey  = "AdminPasswordHash";
    private const string TotpEnabledKey    = "Totp:Enabled";
    private const string TotpSecretKey     = "Totp:Secret";

    private const string SmtpHostKey       = "Smtp:Host";
    private const string SmtpPortKey       = "Smtp:Port";
    private const string SmtpFromKey       = "Smtp:From";
    private const string SmtpUsernameKey   = "Smtp:Username";
    private const string SmtpPasswordKey   = "Smtp:Password";
    private const string SmtpToKey         = "Smtp:To";

    // ── CI Token ──────────────────────────────────────────────────────────────

    public Task<string?> GetCiTokenAsync() => repo.GetAsync(CiTokenKey);

    public async Task<string> RotateCiTokenAsync()
    {
        var token = GenerateToken();
        await repo.SetAsync(CiTokenKey, token);
        return token;
    }

    // ── Admin password ────────────────────────────────────────────────────────

    public Task<string?> GetAdminPasswordHashAsync() => repo.GetAsync(AdminPasswordKey);

    public Task SetAdminPasswordHashAsync(string hash) => repo.SetAsync(AdminPasswordKey, hash);

    // ── TOTP 2FA ──────────────────────────────────────────────────────────────

    public async Task<bool> IsTotpEnabledAsync()
    {
        var val = await repo.GetAsync(TotpEnabledKey);
        return val == "true";
    }

    public async Task<string?> GetTotpSecretAsync()
    {
        var stored = await repo.GetAsync(TotpSecretKey);
        if (string.IsNullOrEmpty(stored)) return stored;

        try
        {
            return protector.Unprotect(stored);
        }
        catch
        {
            // Pre-encryption installs stored the secret as plaintext — fall back
            // so existing 2FA setups keep working.
            return stored;
        }
    }

    public async Task EnableTotpAsync(string secret)
    {
        await repo.SetAsync(TotpSecretKey, protector.Protect(secret));
        await repo.SetAsync(TotpEnabledKey, "true");
    }

    public async Task DisableTotpAsync()
    {
        await repo.SetAsync(TotpEnabledKey, "false");
        await repo.SetAsync(TotpSecretKey, "");
    }

    // ── SMTP (admin-editable at runtime) ──────────────────────────────────────

    public async Task<SmtpConfig> GetSmtpConfigAsync()
    {
        var host     = await repo.GetAsync(SmtpHostKey);
        var portStr  = await repo.GetAsync(SmtpPortKey);
        var port     = int.TryParse(portStr, out var p) ? p : 587;
        var from     = await repo.GetAsync(SmtpFromKey);
        var user     = await repo.GetAsync(SmtpUsernameKey);
        var pwEnc    = await repo.GetAsync(SmtpPasswordKey);
        var to       = await repo.GetAsync(SmtpToKey);

        string? pw = null;
        if (!string.IsNullOrEmpty(pwEnc))
        {
            try { pw = protector.Unprotect(pwEnc); }
            catch { pw = pwEnc; /* pre-encryption value */ }
        }

        return new SmtpConfig(
            Host:     string.IsNullOrWhiteSpace(host) ? null : host,
            Port:     port,
            From:     string.IsNullOrWhiteSpace(from) ? null : from,
            Username: string.IsNullOrWhiteSpace(user) ? null : user,
            Password: pw,
            To:       string.IsNullOrWhiteSpace(to)   ? null : to);
    }

    public async Task SaveSmtpConfigAsync(SmtpConfig cfg, bool replacePassword)
    {
        await repo.SetAsync(SmtpHostKey,     cfg.Host     ?? "");
        await repo.SetAsync(SmtpPortKey,     cfg.Port.ToString());
        await repo.SetAsync(SmtpFromKey,     cfg.From     ?? "");
        await repo.SetAsync(SmtpUsernameKey, cfg.Username ?? "");
        await repo.SetAsync(SmtpToKey,       cfg.To       ?? "");

        if (replacePassword)
        {
            // Empty replacement clears the stored password.
            await repo.SetAsync(SmtpPasswordKey,
                string.IsNullOrEmpty(cfg.Password)
                    ? ""
                    : protector.Protect(cfg.Password));
        }
    }

    public async Task<bool> HasStoredSmtpPasswordAsync() =>
        !string.IsNullOrEmpty(await repo.GetAsync(SmtpPasswordKey));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GenerateToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
