using fiapx_processamento_api.Worker.Domain;
using fiapx_processamento_api.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace fiapx_processamento_api.Worker.Infrastructure.Repositories;

public class VideoRepository : IVideoRepository
{
    private readonly ApplicationDbContext _context;

    public VideoRepository(ApplicationDbContext context) => _context = context;

    public Task<Video?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Videos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task UpdateAsync(Video video, CancellationToken cancellationToken = default)
    {
        _context.Videos.Update(video);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
