using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class ResultsService : IResultsService
{
    private readonly IScrapeRunRepository _scrapeRunRepository;
    private readonly ILinkRepository _linkRepository;
    private readonly IMediaTitleRepository _mediaTitleRepository;
    private readonly IMediaRatingRepository _mediaRatingRepository;
    private readonly IMediaEpisodeRepository _mediaEpisodeRepository;
    private readonly IImdbTitleDetailsRepository _imdbTitleDetailsRepository;
    private readonly IResultsQueryRepository _resultsQueryRepository;
    private readonly ILogger<ResultsService> _logger;

    public ResultsService(
        IScrapeRunRepository scrapeRunRepository,
        ILinkRepository linkRepository,
        IMediaTitleRepository mediaTitleRepository,
        IMediaRatingRepository mediaRatingRepository,
        IMediaEpisodeRepository mediaEpisodeRepository,
        IImdbTitleDetailsRepository imdbTitleDetailsRepository,
        IResultsQueryRepository resultsQueryRepository,
        ILogger<ResultsService> logger)
    {
        _scrapeRunRepository = scrapeRunRepository;
        _linkRepository = linkRepository;
        _mediaTitleRepository = mediaTitleRepository;
        _mediaRatingRepository = mediaRatingRepository;
        _mediaEpisodeRepository = mediaEpisodeRepository;
        _imdbTitleDetailsRepository = imdbTitleDetailsRepository;
        _resultsQueryRepository = resultsQueryRepository;
        _logger = logger;
    }

    public async Task<PagedResult<ResultSummaryDto>> GetResultsAsync(
        ResultFilterDto filter,
        int page,
        int pageSize,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _resultsQueryRepository.GetPagedResultsAsync(
            filter, page, pageSize, sortBy, sortDescending, ct);

        return new PagedResult<ResultSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ResultDetailDto?> GetResultDetailAsync(
        int scrapeRunItemId,
        CancellationToken ct = default)
    {
        var runItem = await _resultsQueryRepository.GetRunItemByIdAsync(scrapeRunItemId, ct);
        if (runItem is null)
            return null;

        var links = await _resultsQueryRepository.GetLinksForRunItemAsync(
            runItem.RunId, runItem.PostUrl, ct);

        var linkDtos = links.Select(l => new LinkDto
        {
            LinkId = l.LinkId,
            Url = l.Url,
            LinkTypeName = l.LinkType?.Name ?? "Unknown",
            ParsedSeason = l.ParsedSeason,
            ParsedEpisode = l.ParsedEpisode,
            IsCurrent = l.IsCurrent
        }).ToList();

        var detail = new ResultDetailDto
        {
            RunItemId = runItem.RunItemId,
            RunId = runItem.RunId,
            PostUrl = runItem.PostUrl,
            Status = runItem.Status.ToString(),
            ProcessedAt = runItem.ProcessedAt,
            Links = linkDtos
        };

        if (runItem.ItemId is not null)
        {
            var mediaTitle = await _mediaTitleRepository.GetByIdAsync(runItem.ItemId.Value, ct);
            if (mediaTitle is not null)
            {
                // IMDB data source has SourceId = 1 by convention
                var rating = await _mediaRatingRepository.GetByMediaIdAndSourceAsync(
                    mediaTitle.MediaId, mediaTitle.SourceId, ct);
                var imdbDetails = await _imdbTitleDetailsRepository.GetByMediaIdAsync(
                    mediaTitle.MediaId, ct);

                int? episodeCount = null;
                IReadOnlyList<int>? seasons = null;
                if (mediaTitle.Type == MediaType.Series)
                {
                    seasons = await _mediaEpisodeRepository.GetSeasonsAsync(mediaTitle.MediaId, ct);
                    var episodes = await _mediaEpisodeRepository.GetByMediaIdAsync(mediaTitle.MediaId, ct);
                    episodeCount = episodes.Count;
                }

                detail = detail with
                {
                    MatchedTitle = mediaTitle.CanonicalTitle,
                    MatchedMediaId = mediaTitle.MediaId,
                    MediaType = mediaTitle.Type.ToString(),
                    Year = mediaTitle.Year,
                    ImdbRating = rating?.Rating,
                    ImdbVoteCount = rating?.VoteCount,
                    Genres = imdbDetails?.GenreString,
                    EpisodeCount = episodeCount,
                    Seasons = seasons
                };
            }
        }

        return detail;
    }
}
