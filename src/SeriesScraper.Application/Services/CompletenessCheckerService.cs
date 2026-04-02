using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class CompletenessCheckerService : ICompletenessCheckerService
{
    private readonly IMediaEpisodeRepository _episodeRepository;
    private readonly ILogger<CompletenessCheckerService> _logger;

    public CompletenessCheckerService(
        IMediaEpisodeRepository episodeRepository,
        ILogger<CompletenessCheckerService> logger)
    {
        _episodeRepository = episodeRepository;
        _logger = logger;
    }

    public async Task<CompletenessStatus> CheckCompletenessAsync(
        int mediaId,
        MediaType mediaType,
        IReadOnlyList<Link> links,
        CancellationToken ct = default)
    {
        if (links.Count == 0)
            return CompletenessStatus.Incomplete;

        // AC#4: Movies — at least one link = Complete
        if (mediaType == MediaType.Movie)
            return CompletenessStatus.Complete;

        // AC#3: TV Series completeness check
        if (mediaType == MediaType.Series)
            return await CheckSeriesCompletenessAsync(mediaId, links, ct);

        return CompletenessStatus.Unknown;
    }

    private async Task<CompletenessStatus> CheckSeriesCompletenessAsync(
        int mediaId,
        IReadOnlyList<Link> links,
        CancellationToken ct)
    {
        // Heuristic: if any link has no episode number (and possibly no season),
        // it may cover a whole series/season archive → treat as Complete
        var hasArchiveLink = links.Any(l => l.ParsedEpisode is null);
        if (hasArchiveLink)
        {
            _logger.LogDebug(
                "Media {MediaId}: found link without episode number, treating as archive → Complete",
                mediaId);
            return CompletenessStatus.Complete;
        }

        var seasons = await _episodeRepository.GetSeasonsAsync(mediaId, ct);
        if (seasons.Count == 0)
        {
            _logger.LogDebug("Media {MediaId}: no episodes in metadata, cannot verify completeness", mediaId);
            return CompletenessStatus.Unknown;
        }

        foreach (var season in seasons)
        {
            var expectedCount = await _episodeRepository.GetEpisodeCountForSeasonAsync(mediaId, season, ct);
            var actualCount = links
                .Where(l => l.ParsedSeason == season && l.ParsedEpisode.HasValue)
                .Select(l => l.ParsedEpisode!.Value)
                .Distinct()
                .Count();

            if (actualCount < expectedCount)
            {
                _logger.LogDebug(
                    "Media {MediaId} Season {Season}: {Actual}/{Expected} episodes → Incomplete",
                    mediaId, season, actualCount, expectedCount);
                return CompletenessStatus.Incomplete;
            }
        }

        return CompletenessStatus.Complete;
    }
}
