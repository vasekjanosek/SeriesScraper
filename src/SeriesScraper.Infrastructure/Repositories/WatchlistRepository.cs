using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

public class WatchlistRepository : IWatchlistRepository
{
    private readonly AppDbContext _context;

    public WatchlistRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken ct = default)
    {
        _context.WatchlistItems.Add(item);
        await _context.SaveChangesAsync(ct);
        return item;
    }

    public async Task<WatchlistItem?> GetByIdAsync(int watchlistItemId, CancellationToken ct = default)
    {
        return await _context.WatchlistItems
            .Include(w => w.MediaTitle)
            .FirstOrDefaultAsync(w => w.WatchlistItemId == watchlistItemId, ct);
    }

    public async Task<IReadOnlyList<WatchlistItem>> GetAllAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _context.WatchlistItems
            .Include(w => w.MediaTitle)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(w => w.IsActive);

        return await query
            .OrderByDescending(w => w.AddedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByMediaTitleIdAsync(int mediaTitleId, CancellationToken ct = default)
    {
        return await _context.WatchlistItems
            .AnyAsync(w => w.MediaTitleId == mediaTitleId, ct);
    }

    public async Task RemoveAsync(int watchlistItemId, CancellationToken ct = default)
    {
        var item = await _context.WatchlistItems.FindAsync(new object[] { watchlistItemId }, ct);
        if (item is not null)
        {
            _context.WatchlistItems.Remove(item);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task UpdateAsync(WatchlistItem item, CancellationToken ct = default)
    {
        _context.WatchlistItems.Update(item);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<(WatchlistItem Item, int NewMatchCount)>> GetItemsWithNewMatchesAsync(
        CancellationToken ct = default)
    {
        // Find active watchlist items with MediaTitleId that have ScrapeRunItems
        // matching (via ItemId) since the last match check time
        var activeItems = await _context.WatchlistItems
            .Include(w => w.MediaTitle)
            .Where(w => w.IsActive && w.MediaTitleId != null)
            .ToListAsync(ct);

        var results = new List<(WatchlistItem Item, int NewMatchCount)>();

        foreach (var item in activeItems)
        {
            var query = _context.ScrapeRunItems
                .Where(sri => sri.ItemId == item.MediaTitleId);

            if (item.LastMatchedAt.HasValue)
                query = query.Where(sri => sri.ProcessedAt > item.LastMatchedAt.Value);

            var count = await query.CountAsync(ct);

            if (count > 0)
            {
                item.LastMatchedAt = DateTime.UtcNow;
                results.Add((item, count));
            }
        }

        if (results.Count > 0)
            await _context.SaveChangesAsync(ct);

        return results.AsReadOnly();
    }
}
