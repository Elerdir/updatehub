using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface ILoginAttemptRepository
{
    Task<LoginAttempt?> GetByIpAsync(string ip);
    Task UpsertAsync(LoginAttempt attempt);
    Task<List<LoginAttempt>> GetAllAsync();
    Task DeleteAsync(string ip);
}
