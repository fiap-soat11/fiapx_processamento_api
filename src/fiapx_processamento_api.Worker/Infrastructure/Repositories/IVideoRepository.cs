using fiapx_processamento_api.Worker.Domain;

namespace fiapx_processamento_api.Worker.Infrastructure.Repositories;

public interface IVideoRepository
{
    Task<Video?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Video video, CancellationToken cancellationToken = default);
}
