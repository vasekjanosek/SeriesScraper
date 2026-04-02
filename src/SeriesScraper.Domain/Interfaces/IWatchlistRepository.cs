using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface IWatchlistRepository
{
    Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken ct = default);
    Task<WatchlistItem?> GetByIdAsync(int watchlistItemId, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistItem>> GetAllAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<bool> ExistsByMediaTitleIdAsync(int mediaTitleId, CancellationToken ct = default);
    Task RemoveAsync(int watchlistItemId, CancellationToken ct = default);
    Task UpdateAsync(WatchlistItem item, CancellationToken ct = default);

    /// <summary>
    /// Finds watchlist items that match new scrape results (by MediaTitleId).
    /// Returns items with their matched scrape run item counts since lastMatchedAt.
    /// </summary>
    Task<IReadOnlyList<(WatchlistItem Item, int NewMatchCount)>> GetItemsWithNewMatchesAsync(
        CancellationToken ct = default);
}
