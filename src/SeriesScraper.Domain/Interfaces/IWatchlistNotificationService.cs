namespace SeriesScraper.Domain.Interfaces;

public interface IWatchlistNotificationService
{
    Task CreateNotificationsForRunAsync(int runId, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistNotificationDto>> GetAllNotificationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistNotificationDto>> GetUnreadNotificationsAsync(CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkAsReadAsync(int notificationId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(CancellationToken ct = default);
}

public record WatchlistNotificationDto
{
    public int WatchlistNotificationId { get; init; }
    public int WatchlistItemId { get; init; }
    public required string WatchlistTitle { get; init; }
    public int LinkId { get; init; }
    public required string LinkUrl { get; init; }
    public string? Language { get; init; }
    public string? Quality { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsRead { get; init; }
}
