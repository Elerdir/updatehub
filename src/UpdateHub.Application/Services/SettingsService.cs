using UpdateHub.Application.Interfaces;

namespace UpdateHub.Application.Services;

public class SettingsService(ISettingsRepository repo)
{
    private const string CiTokenKey        = "CiToken";
    private const string AdminPasswordKey   = "AdminPasswordHash";
    private const string TotpEnabledKey     = "Totp:Enabled";
    private const string TotpSecretKey      = "Totp:Secret";

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

    public Task<string?> GetTotpSecretAsync() => repo.GetAsync(TotpSecretKey);

    public async Task EnableTotpAsync(string secret)
    {
        await repo.SetAsync(TotpSecretKey, secret);
        await repo.SetAsync(TotpEnabledKey, "true");
    }

    public async Task DisableTotpAsync()
    {
        await repo.SetAsync(TotpEnabledKey, "false");
        await repo.SetAsync(TotpSecretKey, "");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GenerateToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
