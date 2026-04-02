using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Orchestrates IMDB title matching for scraped forum content.
/// Parses scraped title strings, extracts embedded years, delegates to
/// IMetadataSource for actual search, and handles disambiguation.
/// </summary>
public class ImdbMatchingService : IImdbMatchingService
{
    private const decimal MinimumConfidenceThreshold = 0.5m;

    private readonly IMetadataSource _metadataSource;
    private readonly ITitleNormalizer _normalizer;
    private readonly ILogger<ImdbMatchingService> _logger;

    public ImdbMatchingService(
        IMetadataSource metadataSource,
        ITitleNormalizer normalizer,
        ILogger<ImdbMatchingService> logger)
    {
        _metadataSource = metadataSource ?? throw new ArgumentNullException(nameof(metadataSource));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MetadataSearchResult?> FindBestMatchAsync(
        string scrapedTitle,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        var matches = await FindMatchesAsync(scrapedTitle, type, cancellationToken);

        var best = matches.FirstOrDefault();
        if (best is null || best.ConfidenceScore < MinimumConfidenceThreshold)
        {
            _logger.LogDebug(
                "No match above threshold {Threshold} for scraped title '{Title}'",
                MinimumConfidenceThreshold, scrapedTitle);
            return null;
        }

        _logger.LogDebug(
            "Best match for '{Title}': {CanonicalTitle} (confidence={Confidence}, externalId={ExternalId})",
            scrapedTitle, best.CanonicalTitle, best.ConfidenceScore, best.ExternalId);

        return best;
    }

    public async Task<IReadOnlyList<MetadataSearchResult>> FindMatchesAsync(
        string scrapedTitle,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scrapedTitle))
            return Array.Empty<MetadataSearchResult>();

        // Extract embedded year from title (e.g. "Breaking Bad (2008)")
        var year = _normalizer.ExtractYear(scrapedTitle);
        var cleanTitle = _normalizer.StripYear(scrapedTitle);

        if (string.IsNullOrWhiteSpace(cleanTitle))
            return Array.Empty<MetadataSearchResult>();

        _logger.LogDebug(
            "Matching scraped title '{Original}' → cleaned='{Clean}', year={Year}, type={Type}",
            scrapedTitle, cleanTitle, year, type);

        var results = await _metadataSource.SearchByTitleAsync(cleanTitle, year, type, cancellationToken);

        _logger.LogDebug(
            "Found {Count} matches for '{Title}'",
            results.Count, scrapedTitle);

        return results;
    }
}
