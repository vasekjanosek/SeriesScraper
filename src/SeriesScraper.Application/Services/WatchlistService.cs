using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class WatchlistService : IWatchlistService
{
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly IMediaTitleRepository _mediaTitleRepository;
    private readonly IMediaRatingRepository _mediaRatingRepository;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(
        IWatchlistRepository watchlistRepository,
        IMediaTitleRepository mediaTitleRepository,
        IMediaRatingRepository mediaRatingRepository,
        ILogger<WatchlistService> logger)
    {
        _watchlistRepository = watchlistRepository;
        _mediaTitleRepository = mediaTitleRepository;
        _mediaRatingRepository = mediaRatingRepository;
        _logger = logger;
    }

    public async Task<WatchlistItemDto> AddToWatchlistByMediaTitleAsync(int mediaTitleId, CancellationToken ct = default)
    {
        var existing = await _watchlistRepository.ExistsByMediaTitleIdAsync(mediaTitleId, ct);
        if (existing)
            throw new InvalidOperationException($"Media title {mediaTitleId} is already on the watchlist.");

        var mediaTitle = await _mediaTitleRepository.GetByIdAsync(mediaTitleId, ct)
            ?? throw new InvalidOperationException($"Media title {mediaTitleId} not found.");

        var item = new WatchlistItem
        {
            MediaTitleId = mediaTitleId,
            CustomTitle = mediaTitle.CanonicalTitle,
            AddedAt = DateTime.UtcNow,
            IsActive = true,
            NotificationPreference = NotificationPreference.None
        };

        var created = await _watchlistRepository.AddAsync(item, ct);
        _logger.LogInformation("Added media title {MediaTitleId} ({Title}) to watchlist", mediaTitleId, mediaTitle.CanonicalTitle);

        return await MapToDto(created, ct);
    }

    public async Task<WatchlistItemDto> AddToWatchlistByCustomTitleAsync(string customTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customTitle))
            throw new ArgumentException("Custom title cannot be empty.", nameof(customTitle));

        var item = new WatchlistItem
        {
            MediaTitleId = null,
            CustomTitle = customTitle.Trim(),
            AddedAt = DateTime.UtcNow,
            IsActive = true,
            NotificationPreference = NotificationPreference.None
        };

        var created = await _watchlistRepository.AddAsync(item, ct);
        _logger.LogInformation("Added custom title '{Title}' to watchlist", customTitle);

        return await MapToDto(created, ct);
    }

    public async Task RemoveFromWatchlistAsync(int watchlistItemId, CancellationToken ct = default)
    {
        await _watchlistRepository.RemoveAsync(watchlistItemId, ct);
        _logger.LogInformation("Removed watchlist item {WatchlistItemId}", watchlistItemId);
    }

    public async Task<IReadOnlyList<WatchlistItemDto>> GetWatchlistAsync(CancellationToken ct = default)
    {
        var items = await _watchlistRepository.GetAllAsync(activeOnly: false, ct);
        var dtos = new List<WatchlistItemDto>(items.Count);

        foreach (var item in items)
        {
            dtos.Add(await MapToDto(item, ct));
        }

        return dtos.AsReadOnly();
    }

    public async Task<bool> IsOnWatchlistAsync(int mediaTitleId, CancellationToken ct = default)
    {
        return await _watchlistRepository.ExistsByMediaTitleIdAsync(mediaTitleId, ct);
    }

    public async Task ToggleActiveAsync(int watchlistItemId, CancellationToken ct = default)
    {
        var item = await _watchlistRepository.GetByIdAsync(watchlistItemId, ct)
            ?? throw new InvalidOperationException($"Watchlist item {watchlistItemId} not found.");

        item.IsActive = !item.IsActive;
        await _watchlistRepository.UpdateAsync(item, ct);
        _logger.LogInformation("Toggled watchlist item {WatchlistItemId} active={IsActive}", watchlistItemId, item.IsActive);
    }

    public async Task UpdateNotificationPreferenceAsync(int watchlistItemId, NotificationPreference preference, CancellationToken ct = default)
    {
        var item = await _watchlistRepository.GetByIdAsync(watchlistItemId, ct)
            ?? throw new InvalidOperationException($"Watchlist item {watchlistItemId} not found.");

        item.NotificationPreference = preference;
        await _watchlistRepository.UpdateAsync(item, ct);
        _logger.LogInformation("Updated notification preference for watchlist item {WatchlistItemId} to {Preference}", watchlistItemId, preference);
    }

    public async Task<IReadOnlyList<WatchlistMatchDto>> CheckNewMatchesAsync(CancellationToken ct = default)
    {
        var matches = await _watchlistRepository.GetItemsWithNewMatchesAsync(ct);

        return matches
            .Select(m => new WatchlistMatchDto
            {
                WatchlistItemId = m.Item.WatchlistItemId,
                CustomTitle = m.Item.CustomTitle,
                NewMatchCount = m.NewMatchCount
            })
            .ToList()
            .AsReadOnly();
    }

    private async Task<WatchlistItemDto> MapToDto(WatchlistItem item, CancellationToken ct)
    {
        decimal? rating = null;
        int? voteCount = null;
        string? mediaType = null;
        int? year = null;

        if (item.MediaTitleId.HasValue)
        {
            var mediaTitle = item.MediaTitle
                ?? await _mediaTitleRepository.GetByIdAsync(item.MediaTitleId.Value, ct);

            if (mediaTitle is not null)
            {
                mediaType = mediaTitle.Type.ToString();
                year = mediaTitle.Year;

                var ratingInfo = await _mediaRatingRepository.GetByMediaIdAndSourceAsync(
                    mediaTitle.MediaId, mediaTitle.SourceId, ct);
                if (ratingInfo is not null)
                {
                    rating = ratingInfo.Rating;
                    voteCount = ratingInfo.VoteCount;
                }
            }
        }

        return new WatchlistItemDto
        {
            WatchlistItemId = item.WatchlistItemId,
            MediaTitleId = item.MediaTitleId,
            CustomTitle = item.CustomTitle,
            IsActive = item.IsActive,
            NotificationPreference = item.NotificationPreference,
            AddedAt = item.AddedAt,
            LastMatchedAt = item.LastMatchedAt,
            MediaType = mediaType,
            Year = year,
            ImdbRating = rating,
            ImdbVoteCount = voteCount
        };
    }
}
