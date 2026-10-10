using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface IPersonalAccessTokenRepository
{
    Task<List<PersonalAccessToken>> GetForUserAsync(Guid userId);
    Task<PersonalAccessToken?> GetByHashAsync(string tokenHash);
    Task CreateAsync(PersonalAccessToken token);
    Task UpdateAsync(PersonalAccessToken token);
}
