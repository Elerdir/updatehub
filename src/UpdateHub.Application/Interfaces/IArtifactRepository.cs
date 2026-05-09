using UpdateHub.Domain.Entities;

namespace UpdateHub.Application.Interfaces;

public interface IArtifactRepository
{
    Task<Artifact?> GetByIdAsync(Guid id);
    Task<Artifact> CreateAsync(Artifact artifact);
    Task DeleteAsync(Artifact artifact);
}
