namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Notification preference for watchlist items.
/// Stored as string in database via HasConversion&lt;string&gt;() per ADR-004.
/// </summary>
public enum NotificationPreference
{
    None,
    OnNewLinks,
    OnNewEpisodes
}
