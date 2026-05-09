using UpdateHub.Domain.Entities;
using UpdateHub.Domain.Enums;

namespace UpdateHub.Application.Interfaces;

public interface IReleaseRepository
{
    Task<Release?> GetByIdAsync(Guid id);
    Task<Release?> GetLatestPublishedAsync(string appSlug, ReleaseChannel channel);
    Task<Release> CreateAsync(Release release);
    Task UpdateAsync(Release release);
    Task DeleteAsync(Release release);
}
