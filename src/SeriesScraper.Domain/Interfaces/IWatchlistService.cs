using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Interfaces;

public interface IWatchlistService
{
    Task<WatchlistItemDto> AddToWatchlistByMediaTitleAsync(int mediaTitleId, CancellationToken ct = default);
    Task<WatchlistItemDto> AddToWatchlistByCustomTitleAsync(string customTitle, CancellationToken ct = default);
    Task RemoveFromWatchlistAsync(int watchlistItemId, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistItemDto>> GetWatchlistAsync(CancellationToken ct = default);
    Task<bool> IsOnWatchlistAsync(int mediaTitleId, CancellationToken ct = default);
    Task ToggleActiveAsync(int watchlistItemId, CancellationToken ct = default);
    Task UpdateNotificationPreferenceAsync(int watchlistItemId, NotificationPreference preference, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistMatchDto>> CheckNewMatchesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MediaTitle>> SearchMediaTitlesAsync(string title, int maxResults, CancellationToken ct = default);
}

public record WatchlistItemDto
{
    public int WatchlistItemId { get; init; }
    public int? MediaTitleId { get; init; }
    public required string CustomTitle { get; init; }
    public bool IsActive { get; init; }
    public NotificationPreference NotificationPreference { get; init; }
    public DateTime AddedAt { get; init; }
    public DateTime? LastMatchedAt { get; init; }

    // MediaTitle info (if linked)
    public string? MediaType { get; init; }
    public int? Year { get; init; }
    public decimal? ImdbRating { get; init; }
    public int? ImdbVoteCount { get; init; }
}

public record WatchlistMatchDto
{
    public int WatchlistItemId { get; init; }
    public required string CustomTitle { get; init; }
    public int NewMatchCount { get; init; }
}
