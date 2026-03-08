using fiapx_processamento_api.Worker.Domain.Entities;
using fiapx_processamento_api.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace fiapx_processamento_api.Worker.Infrastructure.Repositories;

public class VideoRepository : IVideoRepository
{
    private readonly ApplicationDbContext _context;

    public VideoRepository(ApplicationDbContext context) => _context = context;

    public Task<VideoProcessing?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.VideoProcessings.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task UpdateAsync(VideoProcessing video, CancellationToken cancellationToken = default)
    {
        _context.VideoProcessings.Update(video);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<string?> GetUserEmailAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
}
