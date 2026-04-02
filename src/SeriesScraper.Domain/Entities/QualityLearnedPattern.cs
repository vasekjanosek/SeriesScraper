using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Runtime-accumulated quality pattern for ML-based pattern matching.
/// Regex patterns that are learned or seeded for quality extraction (AC#2).
/// </summary>
public class QualityLearnedPattern
{
    public int PatternId { get; set; }
    public required string PatternRegex { get; set; }
    public int DerivedRank { get; set; }
    public int HitCount { get; set; }
    public DateTime? LastMatchedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public required string AlgorithmVersion { get; set; }
    public TokenPolarity Polarity { get; set; }
    public PatternSource Source { get; set; }
}
