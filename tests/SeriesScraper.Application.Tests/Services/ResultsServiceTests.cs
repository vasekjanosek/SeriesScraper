using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class ResultsServiceTests
{
    private readonly IScrapeRunRepository _scrapeRunRepository;
    private readonly ILinkRepository _linkRepository;
    private readonly IMediaTitleRepository _mediaTitleRepository;
    private readonly IMediaRatingRepository _mediaRatingRepository;
    private readonly IMediaEpisodeRepository _mediaEpisodeRepository;
    private readonly IImdbTitleDetailsRepository _imdbTitleDetailsRepository;
    private readonly IResultsQueryRepository _resultsQueryRepository;
    private readonly ILogger<ResultsService> _logger;
    private readonly ResultsService _sut;

    public ResultsServiceTests()
    {
        _scrapeRunRepository = Substitute.For<IScrapeRunRepository>();
        _linkRepository = Substitute.For<ILinkRepository>();
        _mediaTitleRepository = Substitute.For<IMediaTitleRepository>();
        _mediaRatingRepository = Substitute.For<IMediaRatingRepository>();
        _mediaEpisodeRepository = Substitute.For<IMediaEpisodeRepository>();
        _imdbTitleDetailsRepository = Substitute.For<IImdbTitleDetailsRepository>();
        _resultsQueryRepository = Substitute.For<IResultsQueryRepository>();
        _logger = Substitute.For<ILogger<ResultsService>>();

        _sut = new ResultsService(
            _scrapeRunRepository,
            _linkRepository,
            _mediaTitleRepository,
            _mediaRatingRepository,
            _mediaEpisodeRepository,
            _imdbTitleDetailsRepository,
            _resultsQueryRepository,
            _logger);
    }

    // ─── GetResultsAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetResultsAsync_ReturnsPagedResults()
    {
        var summaries = new List<ResultSummaryDto>
        {
            new() { RunItemId = 1, RunId = 10, PostUrl = "http://forum/post/1", Status = "Done", LinkCount = 3 },
            new() { RunItemId = 2, RunId = 10, PostUrl = "http://forum/post/2", Status = "Pending", LinkCount = 0 }
        };

        _resultsQueryRepository.GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((summaries.AsReadOnly(), 2));

        var result = await _sut.GetResultsAsync(new ResultFilterDto(), 1, 20);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetResultsAsync_ClampsPageToMinimum1()
    {
        _resultsQueryRepository.GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ResultSummaryDto>().AsReadOnly() as IReadOnlyList<ResultSummaryDto>, 0));

        var result = await _sut.GetResultsAsync(new ResultFilterDto(), page: -5, pageSize: 20);

        result.Page.Should().Be(1);
        await _resultsQueryRepository.Received(1).GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResultsAsync_ClampsPageSizeToDefault20_WhenInvalid()
    {
        _resultsQueryRepository.GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ResultSummaryDto>().AsReadOnly() as IReadOnlyList<ResultSummaryDto>, 0));

        var result = await _sut.GetResultsAsync(new ResultFilterDto(), page: 1, pageSize: 0);

        result.PageSize.Should().Be(20);
        await _resultsQueryRepository.Received(1).GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResultsAsync_ClampsPageSizeToMaximum100()
    {
        _resultsQueryRepository.GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), 1, 100, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ResultSummaryDto>().AsReadOnly() as IReadOnlyList<ResultSummaryDto>, 0));

        var result = await _sut.GetResultsAsync(new ResultFilterDto(), page: 1, pageSize: 500);

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetResultsAsync_PassesFilterAndSortToRepository()
    {
        var filter = new ResultFilterDto { RunId = 5, ContentType = "Movie", StatusFilter = "Done" };

        _resultsQueryRepository.GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ResultSummaryDto>().AsReadOnly() as IReadOnlyList<ResultSummaryDto>, 0));

        await _sut.GetResultsAsync(filter, 2, 10, "title", true);

        await _resultsQueryRepository.Received(1).GetPagedResultsAsync(
            Arg.Is<ResultFilterDto>(f => f.RunId == 5 && f.ContentType == "Movie" && f.StatusFilter == "Done"),
            2, 10, "title", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResultsAsync_EmptyResults_ReturnsEmptyPage()
    {
        _resultsQueryRepository.GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ResultSummaryDto>().AsReadOnly() as IReadOnlyList<ResultSummaryDto>, 0));

        var result = await _sut.GetResultsAsync(new ResultFilterDto(), 1, 20);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetResultsAsync_CalculatesTotalPagesCorrectly()
    {
        _resultsQueryRepository.GetPagedResultsAsync(
            Arg.Any<ResultFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((new List<ResultSummaryDto>
            {
                new() { RunItemId = 1, RunId = 1, PostUrl = "http://x", Status = "Done", LinkCount = 0 }
            }.AsReadOnly(), 45));

        var result = await _sut.GetResultsAsync(new ResultFilterDto(), 1, 20);

        result.TotalPages.Should().Be(3); // ceil(45/20) = 3
    }

    // ─── GetResultDetailAsync ──────────────────────────────────────

    [Fact]
    public async Task GetResultDetailAsync_RunItemNotFound_ReturnsNull()
    {
        _resultsQueryRepository.GetRunItemByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((ScrapeRunItem?)null);

        var result = await _sut.GetResultDetailAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetResultDetailAsync_NoMediaMatch_ReturnsBasicDetail()
    {
        var runItem = new ScrapeRunItem
        {
            RunItemId = 1,
            RunId = 10,
            PostUrl = "http://forum/post/1",
            Status = ScrapeRunItemStatus.Done,
            ProcessedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            ItemId = null
        };

        _resultsQueryRepository.GetRunItemByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(runItem);
        _resultsQueryRepository.GetLinksForRunItemAsync(10, "http://forum/post/1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Link>().AsReadOnly() as IReadOnlyList<Link>);

        var result = await _sut.GetResultDetailAsync(1);

        result.Should().NotBeNull();
        result!.RunItemId.Should().Be(1);
        result.PostUrl.Should().Be("http://forum/post/1");
        result.Status.Should().Be("Done");
        result.MatchedTitle.Should().BeNull();
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResultDetailAsync_WithMediaMatch_ReturnsFullDetail()
    {
        var runItem = new ScrapeRunItem
        {
            RunItemId = 1,
            RunId = 10,
            PostUrl = "http://forum/post/1",
            Status = ScrapeRunItemStatus.Done,
            ItemId = 42
        };

        var mediaTitle = new MediaTitle
        {
            MediaId = 42,
            CanonicalTitle = "Breaking Bad",
            Year = 2008,
            Type = MediaType.Series,
            SourceId = 1
        };

        var rating = new MediaRating
        {
            MediaId = 42,
            SourceId = 1,
            Rating = 9.5m,
            VoteCount = 2000000
        };

        var imdbDetails = new ImdbTitleDetails
        {
            MediaId = 42,
            Tconst = "tt0903747",
            GenreString = "Crime,Drama,Thriller"
        };

        var episodes = new List<MediaEpisode>
        {
            new() { EpisodeId = 1, MediaId = 42, Season = 1, EpisodeNumber = 1 },
            new() { EpisodeId = 2, MediaId = 42, Season = 1, EpisodeNumber = 2 },
            new() { EpisodeId = 3, MediaId = 42, Season = 2, EpisodeNumber = 1 },
        };

        var seasons = new List<int> { 1, 2 };

        _resultsQueryRepository.GetRunItemByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(runItem);
        _resultsQueryRepository.GetLinksForRunItemAsync(10, "http://forum/post/1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Link>().AsReadOnly() as IReadOnlyList<Link>);
        _mediaTitleRepository.GetByIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(mediaTitle);
        _mediaRatingRepository.GetByMediaIdAndSourceAsync(42, 1, Arg.Any<CancellationToken>())
            .Returns(rating);
        _imdbTitleDetailsRepository.GetByMediaIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(imdbDetails);
        _mediaEpisodeRepository.GetSeasonsAsync(42, Arg.Any<CancellationToken>())
            .Returns(seasons.AsReadOnly());
        _mediaEpisodeRepository.GetByMediaIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(episodes.AsReadOnly());

        var result = await _sut.GetResultDetailAsync(1);

        result.Should().NotBeNull();
        result!.MatchedTitle.Should().Be("Breaking Bad");
        result.MatchedMediaId.Should().Be(42);
        result.MediaType.Should().Be("Series");
        result.Year.Should().Be(2008);
        result.ImdbRating.Should().Be(9.5m);
        result.ImdbVoteCount.Should().Be(2000000);
        result.Genres.Should().Be("Crime,Drama,Thriller");
        result.EpisodeCount.Should().Be(3);
        result.Seasons.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task GetResultDetailAsync_MovieNoEpisodes_SkipsEpisodeLookup()
    {
        var runItem = new ScrapeRunItem
        {
            RunItemId = 1,
            RunId = 10,
            PostUrl = "http://forum/post/1",
            Status = ScrapeRunItemStatus.Done,
            ItemId = 50
        };

        var mediaTitle = new MediaTitle
        {
            MediaId = 50,
            CanonicalTitle = "The Matrix",
            Year = 1999,
            Type = MediaType.Movie,
            SourceId = 1
        };

        _resultsQueryRepository.GetRunItemByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(runItem);
        _resultsQueryRepository.GetLinksForRunItemAsync(10, "http://forum/post/1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Link>().AsReadOnly() as IReadOnlyList<Link>);
        _mediaTitleRepository.GetByIdAsync(50, Arg.Any<CancellationToken>())
            .Returns(mediaTitle);
        _mediaRatingRepository.GetByMediaIdAndSourceAsync(50, 1, Arg.Any<CancellationToken>())
            .Returns((MediaRating?)null);
        _imdbTitleDetailsRepository.GetByMediaIdAsync(50, Arg.Any<CancellationToken>())
            .Returns((ImdbTitleDetails?)null);

        var result = await _sut.GetResultDetailAsync(1);

        result.Should().NotBeNull();
        result!.MatchedTitle.Should().Be("The Matrix");
        result.MediaType.Should().Be("Movie");
        result.EpisodeCount.Should().BeNull();
        result.Seasons.Should().BeNull();

        await _mediaEpisodeRepository.DidNotReceive().GetSeasonsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _mediaEpisodeRepository.DidNotReceive().GetByMediaIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResultDetailAsync_WithLinks_MapsLinksCorrectly()
    {
        var runItem = new ScrapeRunItem
        {
            RunItemId = 1,
            RunId = 10,
            PostUrl = "http://forum/post/1",
            Status = ScrapeRunItemStatus.Done,
            ItemId = null
        };

        var links = new List<Link>
        {
            new()
            {
                LinkId = 100,
                Url = "http://download.com/file1.mkv",
                PostUrl = "http://forum/post/1",
                LinkTypeId = 1,
                ParsedSeason = 1,
                ParsedEpisode = 1,
                IsCurrent = true,
                RunId = 10,
                LinkType = new LinkType { LinkTypeId = 1, Name = "Mega", UrlPattern = "mega.nz", IsSystem = true }
            },
            new()
            {
                LinkId = 101,
                Url = "http://download.com/file2.mkv",
                PostUrl = "http://forum/post/1",
                LinkTypeId = 2,
                ParsedSeason = null,
                ParsedEpisode = null,
                IsCurrent = false,
                RunId = 10,
                LinkType = new LinkType { LinkTypeId = 2, Name = "Uloz.to", UrlPattern = "uloz.to", IsSystem = true }
            }
        };

        _resultsQueryRepository.GetRunItemByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(runItem);
        _resultsQueryRepository.GetLinksForRunItemAsync(10, "http://forum/post/1", Arg.Any<CancellationToken>())
            .Returns(links.AsReadOnly());

        var result = await _sut.GetResultDetailAsync(1);

        result.Should().NotBeNull();
        result!.Links.Should().HaveCount(2);

        result.Links[0].LinkId.Should().Be(100);
        result.Links[0].Url.Should().Be("http://download.com/file1.mkv");
        result.Links[0].LinkTypeName.Should().Be("Mega");
        result.Links[0].ParsedSeason.Should().Be(1);
        result.Links[0].ParsedEpisode.Should().Be(1);
        result.Links[0].IsCurrent.Should().BeTrue();

        result.Links[1].LinkId.Should().Be(101);
        result.Links[1].LinkTypeName.Should().Be("Uloz.to");
        result.Links[1].IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task GetResultDetailAsync_LinkWithNullLinkType_MapsAsUnknown()
    {
        var runItem = new ScrapeRunItem
        {
            RunItemId = 1,
            RunId = 10,
            PostUrl = "http://forum/post/1",
            Status = ScrapeRunItemStatus.Done,
            ItemId = null
        };

        var links = new List<Link>
        {
            new()
            {
                LinkId = 200,
                Url = "http://download.com/file.mkv",
                PostUrl = "http://forum/post/1",
                LinkTypeId = 99,
                IsCurrent = true,
                RunId = 10,
                LinkType = null!
            }
        };

        _resultsQueryRepository.GetRunItemByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(runItem);
        _resultsQueryRepository.GetLinksForRunItemAsync(10, "http://forum/post/1", Arg.Any<CancellationToken>())
            .Returns(links.AsReadOnly());

        var result = await _sut.GetResultDetailAsync(1);

        result!.Links[0].LinkTypeName.Should().Be("Unknown");
    }

    [Fact]
    public async Task GetResultDetailAsync_ItemIdButMediaNotFound_ReturnsDetailWithoutMatch()
    {
        var runItem = new ScrapeRunItem
        {
            RunItemId = 1,
            RunId = 10,
            PostUrl = "http://forum/post/1",
            Status = ScrapeRunItemStatus.Done,
            ItemId = 999 // media ID that doesn't exist
        };

        _resultsQueryRepository.GetRunItemByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(runItem);
        _resultsQueryRepository.GetLinksForRunItemAsync(10, "http://forum/post/1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Link>().AsReadOnly() as IReadOnlyList<Link>);
        _mediaTitleRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((MediaTitle?)null);

        var result = await _sut.GetResultDetailAsync(1);

        result.Should().NotBeNull();
        result!.MatchedTitle.Should().BeNull();
        result.MatchedMediaId.Should().BeNull();
    }

    // ─── PagedResult<T> ────────────────────────────────────────────

    [Fact]
    public void PagedResult_TotalPages_CalculatesCorrectly()
    {
        var result = new PagedResult<ResultSummaryDto>
        {
            TotalCount = 45,
            PageSize = 20,
            Page = 1
        };

        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void PagedResult_TotalPages_ExactDivision()
    {
        var result = new PagedResult<ResultSummaryDto>
        {
            TotalCount = 40,
            PageSize = 20,
            Page = 1
        };

        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public void PagedResult_TotalPages_ZeroPageSize_ReturnsZero()
    {
        var result = new PagedResult<ResultSummaryDto>
        {
            TotalCount = 10,
            PageSize = 0,
            Page = 1
        };

        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void PagedResult_TotalPages_ZeroCount_ReturnsZero()
    {
        var result = new PagedResult<ResultSummaryDto>
        {
            TotalCount = 0,
            PageSize = 20,
            Page = 1
        };

        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void PagedResult_DefaultItems_IsEmpty()
    {
        var result = new PagedResult<ResultSummaryDto>();
        result.Items.Should().BeEmpty();
    }

    // ─── DTO record tests ──────────────────────────────────────────

    [Fact]
    public void ResultFilterDto_DefaultValues_AreNull()
    {
        var filter = new ResultFilterDto();

        filter.RunId.Should().BeNull();
        filter.ContentType.Should().BeNull();
        filter.MinQualityScore.Should().BeNull();
        filter.MinMatchConfidence.Should().BeNull();
        filter.StatusFilter.Should().BeNull();
        filter.TitleSearch.Should().BeNull();
    }

    [Fact]
    public void ResultSummaryDto_RoundTrip()
    {
        var dto = new ResultSummaryDto
        {
            RunItemId = 1,
            RunId = 2,
            PostUrl = "http://url",
            Status = "Done",
            MatchedTitle = "Test",
            MatchedMediaId = 5,
            MediaType = "Movie",
            MatchConfidence = 0.95m,
            QualityScore = 8,
            LinkCount = 3,
            ProcessedAt = DateTime.UtcNow
        };

        dto.RunItemId.Should().Be(1);
        dto.MatchedTitle.Should().Be("Test");
        dto.MatchConfidence.Should().Be(0.95m);
    }

    [Fact]
    public void ResultDetailDto_DefaultLinks_IsEmpty()
    {
        var dto = new ResultDetailDto
        {
            RunItemId = 1,
            RunId = 2,
            PostUrl = "http://url",
            Status = "Done"
        };

        dto.Links.Should().BeEmpty();
    }

    [Fact]
    public void LinkDto_RoundTrip()
    {
        var dto = new LinkDto
        {
            LinkId = 1,
            Url = "http://download.com/file.mkv",
            LinkTypeName = "Mega",
            ParsedSeason = 2,
            ParsedEpisode = 5,
            IsCurrent = true
        };

        dto.LinkId.Should().Be(1);
        dto.LinkTypeName.Should().Be("Mega");
        dto.ParsedSeason.Should().Be(2);
        dto.ParsedEpisode.Should().Be(5);
    }
}
