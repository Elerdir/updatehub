using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface IAppRepository
{
    Task<List<App>> GetAllWithReleasesAsync();
    Task<App?> GetBySlugAsync(string slug);
    Task<App> CreateAsync(App app);
    Task UpdateAsync(App app);
    Task DeleteAsync(App app);
}
