using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository contract for quality token and learned pattern data access.
/// </summary>
public interface IQualityPatternRepository
{
    // ── Quality Tokens ─────────────────────────────────────────────

    Task<IReadOnlyList<QualityToken>> GetActiveTokensAsync(CancellationToken ct = default);
    Task<QualityToken?> GetTokenByIdAsync(int tokenId, CancellationToken ct = default);
    Task<QualityToken?> GetTokenByTextAsync(string tokenText, CancellationToken ct = default);

    // ── Learned Patterns ───────────────────────────────────────────

    Task<IReadOnlyList<QualityLearnedPattern>> GetActivePatternsAsync(CancellationToken ct = default);
    Task<QualityLearnedPattern?> GetPatternByIdAsync(int patternId, CancellationToken ct = default);
    Task AddPatternAsync(QualityLearnedPattern pattern, CancellationToken ct = default);
    Task UpdatePatternAsync(QualityLearnedPattern pattern, CancellationToken ct = default);
    Task<IReadOnlyList<QualityLearnedPattern>> GetPruneCandidatesAsync(int hitCountThreshold, CancellationToken ct = default);
    Task DeactivatePatternsAsync(IEnumerable<int> patternIds, CancellationToken ct = default);
    Task IncrementHitCountAsync(int patternId, CancellationToken ct = default);
}
