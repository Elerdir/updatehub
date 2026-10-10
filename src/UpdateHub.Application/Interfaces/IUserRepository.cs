using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<int> CountAsync();
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
}
