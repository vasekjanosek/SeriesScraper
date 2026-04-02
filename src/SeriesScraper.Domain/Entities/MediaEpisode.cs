namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Episode information for series media.
/// Links to parent MediaTitle (which has Type = Series).
/// Enables episode-level metadata storage and matching.
/// </summary>
public class MediaEpisode
{
    public int EpisodeId { get; set; }
    public int MediaId { get; set; }
    
    /// <summary>
    /// Season number (1-based).
    /// </summary>
    public int Season { get; set; }
    
    /// <summary>
    /// Episode number within the season (1-based).
    /// </summary>
    public int EpisodeNumber { get; set; }
    
    // Navigation properties
    public MediaTitle MediaTitle { get; set; } = null!;
}
