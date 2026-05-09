using UpdateHub.Application.Interfaces;

namespace UpdateHub.Application.Services;

public class SettingsService(ISettingsRepository repo)
{
    private const string CiTokenKey = "CiToken";

    public Task<string?> GetCiTokenAsync() => repo.GetAsync(CiTokenKey);

    public async Task<string> RotateCiTokenAsync()
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        await repo.SetAsync(CiTokenKey, token);
        return token;
    }
}
