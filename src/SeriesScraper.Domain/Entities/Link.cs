namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Extracted download URL from a forum post.
/// Demonstrates partial index on is_current per AC#1 (accumulate-with-flag pattern).
/// </summary>
public class Link
{
    public int LinkId { get; set; }
    public required string Url { get; set; }
    public int LinkTypeId { get; set; }
    
    /// <summary>
    /// Null = parsing failed or not applicable (movies).
    /// </summary>
    public int? ParsedSeason { get; set; }
    
    /// <summary>
    /// Null = parsing failed or not applicable.
    /// </summary>
    public int? ParsedEpisode { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// True = current link. False = historical/superseded.
    /// Partial index: WHERE is_current = true.
    /// </summary>
    public bool IsCurrent { get; set; } = true;
    
    public int RunId { get; set; }
    
    // Navigation properties
    public LinkType LinkType { get; set; } = null!;
    public ScrapeRun Run { get; set; } = null!;
}
