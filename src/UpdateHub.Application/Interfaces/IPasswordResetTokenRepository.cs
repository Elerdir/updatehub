using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash);
    Task CreateAsync(PasswordResetToken token);
    Task UpdateAsync(PasswordResetToken token);
    Task InvalidateAllForUserAsync(Guid userId);
}
