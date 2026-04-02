using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Entities;

/// <summary>
/// A user's watchlist item — tracks a media title they want to follow.
/// MediaTitleId is nullable to support manual custom title entries.
/// </summary>
public class WatchlistItem
{
    public int WatchlistItemId { get; set; }

    /// <summary>
    /// Nullable FK to MediaTitles — null when added as a custom/manual entry.
    /// </summary>
    public int? MediaTitleId { get; set; }

    /// <summary>
    /// User-provided title. Always populated (from MediaTitle.CanonicalTitle or manual entry).
    /// </summary>
    public required string CustomTitle { get; set; }

    public DateTime AddedAt { get; set; }

    /// <summary>
    /// Soft-delete / pause tracking flag.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.None;

    /// <summary>
    /// Last time a new scrape result matched this watchlist item.
    /// </summary>
    public DateTime? LastMatchedAt { get; set; }

    // Navigation properties
    public MediaTitle? MediaTitle { get; set; }
}
