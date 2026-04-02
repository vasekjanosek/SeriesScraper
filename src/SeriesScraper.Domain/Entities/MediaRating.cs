namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Source-specific rating for media (IMDB, CSFD, etc.).
/// Composite key: (media_id, source_id) allows multiple ratings per media.
/// </summary>
public class MediaRating
{
    public int MediaId { get; set; }
    public int SourceId { get; set; }
    
    /// <summary>
    /// Rating value (e.g., 7.5 for IMDB 7.5/10).
    /// Schema permits decimal(3,1) for ratings like 9.5.
    /// </summary>
    public decimal Rating { get; set; }
    
    /// <summary>
    /// Total number of votes/ratings (e.g., IMDB vote count).
    /// </summary>
    public int VoteCount { get; set; }
    
    // Navigation properties
    public MediaTitle MediaTitle { get; set; } = null!;
    public DataSource DataSource { get; set; } = null!;
}
