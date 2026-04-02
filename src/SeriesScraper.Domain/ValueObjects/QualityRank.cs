using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Result of quality extraction from a forum post.
/// Contains the overall quality score, all matched tokens, and a confidence rating.
/// </summary>
public sealed record QualityRank
{
    /// <summary>
    /// The overall quality score (highest quality_rank among matches, adjusted for polarity).
    /// Zero when no quality indicators are found.
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// All quality tokens/patterns that matched in the post text.
    /// </summary>
    public IReadOnlyList<string> MatchedTokens { get; init; } = [];

    /// <summary>
    /// Confidence in the extraction result (0.0–1.0).
    /// Higher values indicate more matching evidence.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// The polarity of the best (winning) match.
    /// Null when no matches are found.
    /// </summary>
    public TokenPolarity? BestMatchPolarity { get; init; }

    /// <summary>
    /// Creates a QualityRank representing no quality information found.
    /// </summary>
    public static QualityRank None => new()
    {
        Score = 0,
        MatchedTokens = [],
        Confidence = 0.0,
        BestMatchPolarity = null
    };
}
