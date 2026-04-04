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
    /// Source post URL — used for accumulate-with-flag scoping per AC#7.
    /// </summary>
    public required string PostUrl { get; set; }
    
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
    
    /// <summary>
    /// ISO 639-1 language code(s) parsed from the thread title.
    /// Comma-separated when multiple languages are present (e.g. "cs,en").
    /// Null if no language tag was detected.
    /// </summary>
    public string? Language { get; set; }
    
    public int RunId { get; set; }
    
    /// <summary>
    /// Quality descriptor extracted from the post (e.g., "1080p BluRay"). Null = not yet extracted.
    /// </summary>
    public string? Quality { get; set; }
    
    // Navigation properties
    public LinkType LinkType { get; set; } = null!;
    public ScrapeRun Run { get; set; } = null!;
}
