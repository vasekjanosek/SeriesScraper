namespace SeriesScraper.Domain.Entities;

/// <summary>
/// IMDB-specific metadata for a media title.
/// One-to-one relationship with MediaTitle (media_id is PK and FK).
/// tconst is NEVER referenced from Watchlist, ScrapeRun, or Link entities — ALL references use media_id.
/// </summary>
public class ImdbTitleDetails
{
    /// <summary>
    /// FK to MediaTitles.media_id. Also serves as PK (1:1 relationship).
    /// </summary>
    public int MediaId { get; set; }
    
    /// <summary>
    /// IMDB title ID (e.g., "tt0133093"). Unique across all IMDB titles.
    /// </summary>
    public required string Tconst { get; set; }
    
    /// <summary>
    /// Comma-separated genre list from IMDB (e.g., "Action,Sci-Fi").
    /// Null = genres unknown or not available.
    /// </summary>
    public string? GenreString { get; set; }
    
    // Navigation properties
    public MediaTitle MediaTitle { get; set; } = null!;
}
