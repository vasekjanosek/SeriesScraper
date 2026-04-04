namespace SeriesScraper.Domain.Entities;

public class WatchlistNotification
{
    public int WatchlistNotificationId { get; set; }
    public int WatchlistItemId { get; set; }
    public int LinkId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }

    // Navigation properties
    public WatchlistItem WatchlistItem { get; set; } = null!;
    public Link Link { get; set; } = null!;
}
