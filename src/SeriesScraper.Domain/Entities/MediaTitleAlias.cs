namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Alternate titles for media (localized, region-specific, or alternate names).
/// Enables searching/matching media by any of its known aliases.
/// </summary>
public class MediaTitleAlias
{
    public int AliasId { get; set; }
    public int MediaId { get; set; }
    public required string AliasTitle { get; set; }
    
    /// <summary>
    /// ISO 639-1 language code (e.g., "en", "cs"). Null = language unknown.
    /// </summary>
    public string? Language { get; set; }
    
    /// <summary>
    /// ISO 3166-1 region code (e.g., "US", "CZ"). Null = region unknown.
    /// </summary>
    public string? Region { get; set; }
    
    // Navigation properties
    public MediaTitle MediaTitle { get; set; } = null!;
}
