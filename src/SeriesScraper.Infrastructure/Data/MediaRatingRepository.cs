using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Data;

public class MediaRatingRepository : IMediaRatingRepository
{
    private readonly AppDbContext _context;

    public MediaRatingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MediaRating?> GetByMediaIdAndSourceAsync(
        int mediaId, int sourceId, CancellationToken ct = default)
    {
        return await _context.MediaRatings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.MediaId == mediaId && r.SourceId == sourceId, ct);
    }
}
