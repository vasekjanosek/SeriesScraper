using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Canonical media title layer.
/// ALL application logic references media_id, NEVER source-specific IDs (tconst).
/// Demonstrates string enum conversion per AC#3.
/// </summary>
public class MediaTitle
{
    public int MediaId { get; set; }
    public required string CanonicalTitle { get; set; }
    
    /// <summary>
    /// Null = year unknown or not applicable.
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// Media type enum stored as string via HasConversion&lt;string&gt;().
    /// </summary>
    public MediaType Type { get; set; }
    
    public int SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public DataSource DataSource { get; set; } = null!;
}
