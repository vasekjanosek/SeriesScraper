using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Entities;

/// <summary>
/// User-editable quality token registry.
/// Demonstrates partial index on is_active per AC#1.
/// </summary>
public class QualityToken
{
    public int TokenId { get; set; }
    public required string TokenText { get; set; }
    public int QualityRank { get; set; }
    
    /// <summary>
    /// Polarity enum stored as string via HasConversion&lt;string&gt;().
    /// </summary>
    public TokenPolarity Polarity { get; set; }
    
    public bool IsActive { get; set; } = true;
}
