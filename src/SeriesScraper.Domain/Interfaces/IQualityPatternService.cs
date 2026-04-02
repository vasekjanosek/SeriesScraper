using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Service contract for quality pattern management.
/// Manages quality tokens and learned patterns for the extraction engine (#26).
/// </summary>
public interface IQualityPatternService
{
    /// <summary>
    /// Gets all active quality tokens ordered by quality rank descending.
    /// </summary>
    Task<IReadOnlyList<QualityToken>> GetActiveTokensAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all active learned patterns.
    /// </summary>
    Task<IReadOnlyList<QualityLearnedPattern>> GetActivePatternsAsync(CancellationToken ct = default);

    /// <summary>
    /// Records a new learned pattern from the extraction engine.
    /// </summary>
    Task<QualityLearnedPattern> AddLearnedPatternAsync(string patternRegex, int derivedRank,
        Enums.TokenPolarity polarity, string algorithmVersion, CancellationToken ct = default);

    /// <summary>
    /// Increments the hit count for a pattern and updates last_matched_at.
    /// </summary>
    Task RecordPatternHitAsync(int patternId, CancellationToken ct = default);

    /// <summary>
    /// Returns patterns with hit_count below the configured QualityPruningThreshold.
    /// </summary>
    Task<IReadOnlyList<QualityLearnedPattern>> GetPruneCandidatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Deactivates the specified patterns (soft-delete by setting is_active = false).
    /// </summary>
    Task DeactivatePatternsAsync(IEnumerable<int> patternIds, CancellationToken ct = default);

    /// <summary>
    /// Gets the configured pruning threshold from Settings.
    /// </summary>
    Task<int> GetPruningThresholdAsync(CancellationToken ct = default);
}
