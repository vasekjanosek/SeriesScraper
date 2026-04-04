using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface IWatchlistNotificationRepository
{
    Task<WatchlistNotification> AddAsync(WatchlistNotification notification, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<WatchlistNotification> notifications, CancellationToken ct = default);
    Task<WatchlistNotification?> GetByIdAsync(int notificationId, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistNotification>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistNotification>> GetUnreadAsync(CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkAsReadAsync(int notificationId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(CancellationToken ct = default);
}
