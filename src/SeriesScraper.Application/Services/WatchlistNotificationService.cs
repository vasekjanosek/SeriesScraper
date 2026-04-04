using Microsoft.Extensions.Logging;
using SeriesScraper.Application.Utilities;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class WatchlistNotificationService : IWatchlistNotificationService
{
    private readonly IWatchlistNotificationRepository _notificationRepository;
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly ILinkRepository _linkRepository;
    private readonly ILogger<WatchlistNotificationService> _logger;

    public WatchlistNotificationService(
        IWatchlistNotificationRepository notificationRepository,
        IWatchlistRepository watchlistRepository,
        ILinkRepository linkRepository,
        ILogger<WatchlistNotificationService> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _watchlistRepository = watchlistRepository ?? throw new ArgumentNullException(nameof(watchlistRepository));
        _linkRepository = linkRepository ?? throw new ArgumentNullException(nameof(linkRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CreateNotificationsForRunAsync(int runId, CancellationToken ct = default)
    {
        var watchlistItems = await _watchlistRepository.GetAllAsync(activeOnly: true, ct);
        if (watchlistItems.Count == 0)
        {
            _logger.LogDebug("No active watchlist items, skipping notification check for run {RunId}", runId);
            return;
        }

        var runLinks = await _linkRepository.GetCurrentByRunIdAsync(runId, ct);
        if (runLinks.Count == 0)
        {
            _logger.LogDebug("No links found for run {RunId}, skipping notification check", runId);
            return;
        }

        // Build a lookup from CustomTitle (normalized) to watchlist items that want notifications
        var watchlistByTitle = watchlistItems
            .Where(w => w.MediaTitleId.HasValue && w.NotificationPreference != Domain.Enums.NotificationPreference.None)
            .GroupBy(w => w.CustomTitle.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        // Custom title watchlist items (no MediaTitleId) — match by title substring in PostUrl
        var customWatchlistItems = watchlistItems
            .Where(w => !w.MediaTitleId.HasValue && w.NotificationPreference != Domain.Enums.NotificationPreference.None)
            .ToList();

        var notifications = new List<WatchlistNotification>();

        foreach (var link in runLinks)
        {
            // Try matching via post URL title extraction
            var extractedTitle = UrlTitleExtractor.ExtractFrom(link.PostUrl);
            if (extractedTitle is null)
                continue;

            var normalizedTitle = extractedTitle.ToUpperInvariant();

            // Match against watchlist items by title similarity
            foreach (var (titleKey, items) in watchlistByTitle)
            {
                if (normalizedTitle.Contains(titleKey) || titleKey.Contains(normalizedTitle))
                {
                    foreach (var item in items)
                    {
                        notifications.Add(new WatchlistNotification
                        {
                            WatchlistItemId = item.WatchlistItemId,
                            LinkId = link.LinkId,
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }
                }
            }

            // Match custom title watchlist items by substring match in PostUrl
            foreach (var item in customWatchlistItems)
            {
                var customNorm = item.CustomTitle.ToUpperInvariant();
                if (normalizedTitle.Contains(customNorm) || customNorm.Contains(normalizedTitle))
                {
                    notifications.Add(new WatchlistNotification
                    {
                        WatchlistItemId = item.WatchlistItemId,
                        LinkId = link.LinkId,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
            }
        }

        // Deduplicate by (WatchlistItemId, LinkId)
        var uniqueNotifications = notifications
            .GroupBy(n => (n.WatchlistItemId, n.LinkId))
            .Select(g => g.First())
            .ToList();

        if (uniqueNotifications.Count > 0)
        {
            await _notificationRepository.AddRangeAsync(uniqueNotifications, ct);
            _logger.LogInformation(
                "Created {Count} watchlist notifications for run {RunId}",
                uniqueNotifications.Count, runId);
        }
        else
        {
            _logger.LogDebug("No watchlist matches found for run {RunId}", runId);
        }
    }

    public async Task<IReadOnlyList<WatchlistNotificationDto>> GetAllNotificationsAsync(CancellationToken ct = default)
    {
        var notifications = await _notificationRepository.GetAllAsync(ct);
        return notifications.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<WatchlistNotificationDto>> GetUnreadNotificationsAsync(CancellationToken ct = default)
    {
        var notifications = await _notificationRepository.GetUnreadAsync(ct);
        return notifications.Select(MapToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        return await _notificationRepository.GetUnreadCountAsync(ct);
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        await _notificationRepository.MarkAsReadAsync(notificationId, ct);
        _logger.LogDebug("Marked notification {NotificationId} as read", notificationId);
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        await _notificationRepository.MarkAllAsReadAsync(ct);
        _logger.LogInformation("Marked all notifications as read");
    }

    private static WatchlistNotificationDto MapToDto(WatchlistNotification n) => new()
    {
        WatchlistNotificationId = n.WatchlistNotificationId,
        WatchlistItemId = n.WatchlistItemId,
        WatchlistTitle = n.WatchlistItem?.CustomTitle ?? "Unknown",
        LinkId = n.LinkId,
        LinkUrl = n.Link?.Url ?? "",
        Language = n.Link?.Language,
        Quality = n.Link?.Quality,
        CreatedAt = n.CreatedAt,
        IsRead = n.IsRead
    };
}
