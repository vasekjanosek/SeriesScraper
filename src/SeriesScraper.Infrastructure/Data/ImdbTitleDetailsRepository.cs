using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Data;

public class ImdbTitleDetailsRepository : IImdbTitleDetailsRepository
{
    private readonly AppDbContext _context;

    public ImdbTitleDetailsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ImdbTitleDetails?> GetByTconstAsync(string tconst, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tconst))
            return null;

        return await _context.ImdbTitleDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Tconst == tconst, ct);
    }

    public async Task<ImdbTitleDetails?> GetByMediaIdAsync(int mediaId, CancellationToken ct = default)
    {
        return await _context.ImdbTitleDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MediaId == mediaId, ct);
    }

    public async Task<IReadOnlyDictionary<int, ImdbTitleDetails>> GetByMediaIdsAsync(
        IReadOnlyList<int> mediaIds, CancellationToken ct = default)
    {
        if (mediaIds.Count == 0)
            return new Dictionary<int, ImdbTitleDetails>();

        return await _context.ImdbTitleDetails
            .Where(d => mediaIds.Contains(d.MediaId))
            .AsNoTracking()
            .ToDictionaryAsync(d => d.MediaId, ct);
    }
}
