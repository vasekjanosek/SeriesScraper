using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Manages quality tokens and learned patterns.
/// Reads the QualityPruningThreshold from Settings to determine prune candidates.
/// </summary>
public class QualityPatternService : IQualityPatternService
{
    private const string PruningThresholdKey = "QualityPruningThreshold";
    private const int DefaultPruningThreshold = 5;

    private readonly IQualityPatternRepository _repository;
    private readonly ISettingRepository _settings;

    public QualityPatternService(IQualityPatternRepository repository, ISettingRepository settings)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<IReadOnlyList<QualityToken>> GetActiveTokensAsync(CancellationToken ct = default)
    {
        return await _repository.GetActiveTokensAsync(ct);
    }

    public async Task<IReadOnlyList<QualityLearnedPattern>> GetActivePatternsAsync(CancellationToken ct = default)
    {
        return await _repository.GetActivePatternsAsync(ct);
    }

    public async Task<QualityLearnedPattern> AddLearnedPatternAsync(
        string patternRegex, int derivedRank, TokenPolarity polarity,
        string algorithmVersion, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patternRegex);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmVersion);

        var pattern = new QualityLearnedPattern
        {
            PatternRegex = patternRegex,
            DerivedRank = derivedRank,
            Polarity = polarity,
            AlgorithmVersion = algorithmVersion,
            Source = PatternSource.Learned,
            HitCount = 0,
            IsActive = true,
            LastMatchedAt = null
        };

        await _repository.AddPatternAsync(pattern, ct);
        return pattern;
    }

    public async Task RecordPatternHitAsync(int patternId, CancellationToken ct = default)
    {
        await _repository.IncrementHitCountAsync(patternId, ct);
    }

    public async Task<IReadOnlyList<QualityLearnedPattern>> GetPruneCandidatesAsync(CancellationToken ct = default)
    {
        var threshold = await GetPruningThresholdAsync(ct);
        return await _repository.GetPruneCandidatesAsync(threshold, ct);
    }

    public async Task DeactivatePatternsAsync(IEnumerable<int> patternIds, CancellationToken ct = default)
    {
        await _repository.DeactivatePatternsAsync(patternIds, ct);
    }

    public async Task<int> GetPruningThresholdAsync(CancellationToken ct = default)
    {
        var value = await _settings.GetValueAsync(PruningThresholdKey, ct);
        return int.TryParse(value, out var threshold) ? threshold : DefaultPruningThreshold;
    }
}
