using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Data;

public class MediaEpisodeRepository : IMediaEpisodeRepository
{
    private readonly AppDbContext _context;

    public MediaEpisodeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MediaEpisode>> GetByMediaIdAsync(int mediaId, CancellationToken ct = default)
    {
        return await _context.MediaEpisodes
            .Where(e => e.MediaId == mediaId)
            .OrderBy(e => e.Season)
            .ThenBy(e => e.EpisodeNumber)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> GetEpisodeCountForSeasonAsync(int mediaId, int season, CancellationToken ct = default)
    {
        return await _context.MediaEpisodes
            .CountAsync(e => e.MediaId == mediaId && e.Season == season, ct);
    }

    public async Task<IReadOnlyList<int>> GetSeasonsAsync(int mediaId, CancellationToken ct = default)
    {
        return await _context.MediaEpisodes
            .Where(e => e.MediaId == mediaId)
            .Select(e => e.Season)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
    }
}
