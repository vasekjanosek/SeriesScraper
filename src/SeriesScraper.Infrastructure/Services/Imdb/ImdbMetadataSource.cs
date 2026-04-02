using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Infrastructure.Services.Imdb;

/// <summary>
/// IMDB implementation of IMetadataSource.
/// Searches imported IMDB data (MediaTitle, MediaTitleAlias, ImdbTitleDetails)
/// using fuzzy/normalized string matching with confidence scoring.
/// </summary>
public class ImdbMetadataSource : IMetadataSource
{
    public const int ImdbSourceId = 1;
    public const int MaxCandidates = 100;
    public const decimal AliasPenalty = 0.02m;
    public const decimal YearBonus = 0.05m;

    private readonly IMediaTitleRepository _titleRepo;
    private readonly IMediaEpisodeRepository _episodeRepo;
    private readonly IMediaRatingRepository _ratingRepo;
    private readonly IImdbTitleDetailsRepository _imdbDetailsRepo;
    private readonly ITitleNormalizer _normalizer;
    private readonly ILogger<ImdbMetadataSource> _logger;

    public ImdbMetadataSource(
        IMediaTitleRepository titleRepo,
        IMediaEpisodeRepository episodeRepo,
        IMediaRatingRepository ratingRepo,
        IImdbTitleDetailsRepository imdbDetailsRepo,
        ITitleNormalizer normalizer,
        ILogger<ImdbMetadataSource> logger)
    {
        _titleRepo = titleRepo ?? throw new ArgumentNullException(nameof(titleRepo));
        _episodeRepo = episodeRepo ?? throw new ArgumentNullException(nameof(episodeRepo));
        _ratingRepo = ratingRepo ?? throw new ArgumentNullException(nameof(ratingRepo));
        _imdbDetailsRepo = imdbDetailsRepo ?? throw new ArgumentNullException(nameof(imdbDetailsRepo));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string SourceIdentifier => "imdb";

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchByTitleAsync(
        string query,
        int? year = null,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<MetadataSearchResult>();

        var normalizedQuery = _normalizer.Normalize(query);
        if (string.IsNullOrEmpty(normalizedQuery))
            return Array.Empty<MetadataSearchResult>();

        var mediaType = ParseMediaType(type);

        // Get candidates from canonical titles
        var titleCandidates = await _titleRepo.SearchByTitleAsync(
            normalizedQuery, mediaType, year, MaxCandidates, cancellationToken);

        // Get candidates from aliases
        var aliasCandidates = await _titleRepo.SearchByAliasAsync(
            normalizedQuery, mediaType, year, MaxCandidates, cancellationToken);

        // Collect all unique media IDs
        var allMediaIds = titleCandidates.Select(t => t.MediaId)
            .Union(aliasCandidates.Select(a => a.MediaId))
            .Distinct()
            .ToList();

        if (allMediaIds.Count == 0)
            return Array.Empty<MetadataSearchResult>();

        // Batch-load IMDB details for external IDs
        var imdbDetails = await _imdbDetailsRepo.GetByMediaIdsAsync(allMediaIds, cancellationToken);

        var results = new Dictionary<int, MetadataSearchResult>();

        // Score canonical title matches
        foreach (var title in titleCandidates)
        {
            if (!imdbDetails.TryGetValue(title.MediaId, out var details))
                continue;

            var similarity = _normalizer.ComputeSimilarity(query, title.CanonicalTitle);
            var score = ApplyYearBonus(similarity, year, title.Year);

            UpdateBestResult(results, title, details.Tconst, score);
        }

        // Score alias matches
        foreach (var alias in aliasCandidates)
        {
            if (!imdbDetails.TryGetValue(alias.MediaId, out var details))
                continue;

            var similarity = _normalizer.ComputeSimilarity(query, alias.AliasTitle);
            var score = Math.Max(0m, ApplyYearBonus(similarity, year, alias.MediaTitle?.Year) - AliasPenalty);

            // Get the MediaTitle (from canonical results or the alias navigation)
            var mediaTitle = titleCandidates.FirstOrDefault(t => t.MediaId == alias.MediaId)
                             ?? alias.MediaTitle;
            if (mediaTitle is null)
                continue;

            UpdateBestResult(results, mediaTitle, details.Tconst, score);
        }

        return results.Values
            .OrderByDescending(r => r.ConfidenceScore)
            .ToList();
    }

    public async Task<MetadataSearchResult?> SearchByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        var details = await _imdbDetailsRepo.GetByTconstAsync(externalId, cancellationToken);
        if (details is null)
            return null;

        var title = await _titleRepo.GetByIdAsync(details.MediaId, cancellationToken);
        if (title is null)
            return null;

        return new MetadataSearchResult
        {
            MediaId = title.MediaId,
            CanonicalTitle = title.CanonicalTitle,
            Year = title.Year,
            Type = MediaTypeToString(title.Type),
            ConfidenceScore = 1.0m,
            ExternalId = details.Tconst
        };
    }

    public async Task<IReadOnlyList<EpisodeInfo>> GetEpisodeListAsync(
        int titleId,
        int? season = null,
        CancellationToken cancellationToken = default)
    {
        var episodes = await _episodeRepo.GetByMediaIdAsync(titleId, cancellationToken);

        if (season.HasValue)
            episodes = episodes.Where(e => e.Season == season.Value).ToList();

        return episodes.Select(e => new EpisodeInfo
        {
            Season = e.Season,
            EpisodeNumber = e.EpisodeNumber,
            Title = null // IMDB episode titles not stored in MediaEpisode entity
        }).ToList();
    }

    public async Task<RatingInfo?> GetRatingsAsync(
        int titleId,
        CancellationToken cancellationToken = default)
    {
        var rating = await _ratingRepo.GetByMediaIdAndSourceAsync(titleId, ImdbSourceId, cancellationToken);
        if (rating is null)
            return null;

        return new RatingInfo
        {
            Rating = rating.Rating,
            VoteCount = rating.VoteCount
        };
    }

    private static decimal ApplyYearBonus(decimal similarity, int? queryYear, int? candidateYear)
    {
        if (queryYear.HasValue && candidateYear.HasValue && queryYear.Value == candidateYear.Value)
            return Math.Min(1.0m, similarity + YearBonus);

        return similarity;
    }

    private static void UpdateBestResult(
        Dictionary<int, MetadataSearchResult> results,
        MediaTitle title,
        string tconst,
        decimal score)
    {
        if (results.TryGetValue(title.MediaId, out var existing) && existing.ConfidenceScore >= score)
            return;

        results[title.MediaId] = new MetadataSearchResult
        {
            MediaId = title.MediaId,
            CanonicalTitle = title.CanonicalTitle,
            Year = title.Year,
            Type = MediaTypeToString(title.Type),
            ConfidenceScore = score,
            ExternalId = tconst
        };
    }

    public static MediaType? ParseMediaType(string? type)
    {
        if (string.IsNullOrEmpty(type))
            return null;

        return type.ToLowerInvariant() switch
        {
            "movie" => MediaType.Movie,
            "series" => MediaType.Series,
            "episode" => MediaType.Episode,
            _ => null
        };
    }

    public static string MediaTypeToString(MediaType type)
    {
        return type switch
        {
            MediaType.Movie => "movie",
            MediaType.Series => "series",
            MediaType.Episode => "episode",
            _ => type.ToString().ToLowerInvariant()
        };
    }
}
