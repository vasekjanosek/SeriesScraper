using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Data;

public class MediaTitleRepository : IMediaTitleRepository
{
    private readonly AppDbContext _context;

    public MediaTitleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MediaTitle>> SearchByTitleAsync(
        string normalizedSearchTerm,
        MediaType? type = null,
        int? year = null,
        int maxResults = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearchTerm))
            return Array.Empty<MediaTitle>();

        var lowerTerm = normalizedSearchTerm.ToLowerInvariant();

        var query = _context.MediaTitles.AsQueryable();

        query = query.Where(t => t.CanonicalTitle.ToLower().Contains(lowerTerm));

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

        if (year.HasValue)
            query = query.Where(t => t.Year == year.Value);

        return await query
            .OrderBy(t => t.CanonicalTitle)
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaTitleAlias>> SearchByAliasAsync(
        string normalizedSearchTerm,
        MediaType? type = null,
        int? year = null,
        int maxResults = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearchTerm))
            return Array.Empty<MediaTitleAlias>();

        var lowerTerm = normalizedSearchTerm.ToLowerInvariant();

        var query = _context.MediaTitleAliases
            .Include(a => a.MediaTitle)
            .Where(a => a.AliasTitle.ToLower().Contains(lowerTerm));

        if (type.HasValue)
            query = query.Where(a => a.MediaTitle.Type == type.Value);

        if (year.HasValue)
            query = query.Where(a => a.MediaTitle.Year == year.Value);

        return await query
            .OrderBy(a => a.AliasTitle)
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<MediaTitle?> GetByIdAsync(int mediaId, CancellationToken ct = default)
    {
        return await _context.MediaTitles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.MediaId == mediaId, ct);
    }
}
