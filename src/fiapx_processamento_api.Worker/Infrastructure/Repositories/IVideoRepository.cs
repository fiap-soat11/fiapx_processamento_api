using fiapx_processamento_api.Worker.Domain.Entities;

namespace fiapx_processamento_api.Worker.Infrastructure.Repositories;

public interface IVideoRepository
{
    Task<VideoProcessing?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAsync(VideoProcessing video, CancellationToken cancellationToken = default);
    Task<string?> GetUserEmailAsync(int userId, CancellationToken cancellationToken = default);
}
