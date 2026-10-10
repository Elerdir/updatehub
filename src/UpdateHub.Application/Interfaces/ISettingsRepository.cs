namespace UpdateHub.Application.Interfaces;

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}
