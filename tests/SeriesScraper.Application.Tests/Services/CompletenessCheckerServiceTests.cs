using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class CompletenessCheckerServiceTests
{
    private readonly Mock<IMediaEpisodeRepository> _episodeRepoMock = new();
    private readonly Mock<ILogger<CompletenessCheckerService>> _loggerMock = new();
    private readonly CompletenessCheckerService _sut;

    public CompletenessCheckerServiceTests()
    {
        _sut = new CompletenessCheckerService(_episodeRepoMock.Object, _loggerMock.Object);
    }

    // ── Movies ───────────────────────────────────────────────

    [Fact]
    public async Task CheckCompleteness_Movie_NoLinks_ReturnsIncomplete()
    {
        var result = await _sut.CheckCompletenessAsync(1, MediaType.Movie, Array.Empty<Link>());

        result.Should().Be(CompletenessStatus.Incomplete);
    }

    [Fact]
    public async Task CheckCompleteness_Movie_WithLink_ReturnsComplete()
    {
        var links = new List<Link>
        {
            CreateLink("https://example.com/movie.zip")
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Movie, links);

        result.Should().Be(CompletenessStatus.Complete);
    }

    [Fact]
    public async Task CheckCompleteness_Movie_MultipleLinks_ReturnsComplete()
    {
        var links = new List<Link>
        {
            CreateLink("https://example.com/movie-720p.zip"),
            CreateLink("https://example.com/movie-1080p.zip")
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Movie, links);

        result.Should().Be(CompletenessStatus.Complete);
    }

    // ── Series — Complete ────────────────────────────────────

    [Fact]
    public async Task CheckCompleteness_Series_AllEpisodesPresent_ReturnsComplete()
    {
        _episodeRepoMock.Setup(r => r.GetSeasonsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 1 });
        _episodeRepoMock.Setup(r => r.GetEpisodeCountForSeasonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var links = new List<Link>
        {
            CreateLink("https://example.com/S01E01.zip", season: 1, episode: 1),
            CreateLink("https://example.com/S01E02.zip", season: 1, episode: 2),
            CreateLink("https://example.com/S01E03.zip", season: 1, episode: 3)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Complete);
    }

    [Fact]
    public async Task CheckCompleteness_Series_ArchiveLink_ReturnsComplete()
    {
        // Heuristic: link without episode number → archive → Complete
        var links = new List<Link>
        {
            CreateLink("https://example.com/series-complete.zip", season: null, episode: null)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Complete);
    }

    [Fact]
    public async Task CheckCompleteness_Series_SeasonArchiveLink_ReturnsComplete()
    {
        // Link with season but no episode → season archive → Complete
        var links = new List<Link>
        {
            CreateLink("https://example.com/S01-complete.zip", season: 1, episode: null)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Complete);
    }

    // ── Series — Incomplete ─────────────────────────────────

    [Fact]
    public async Task CheckCompleteness_Series_MissingEpisodes_ReturnsIncomplete()
    {
        _episodeRepoMock.Setup(r => r.GetSeasonsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 1 });
        _episodeRepoMock.Setup(r => r.GetEpisodeCountForSeasonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var links = new List<Link>
        {
            CreateLink("https://example.com/S01E01.zip", season: 1, episode: 1),
            CreateLink("https://example.com/S01E03.zip", season: 1, episode: 3)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Incomplete);
    }

    [Fact]
    public async Task CheckCompleteness_Series_NoLinks_ReturnsIncomplete()
    {
        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, Array.Empty<Link>());

        result.Should().Be(CompletenessStatus.Incomplete);
    }

    // ── Series — Unknown ─────────────────────────────────────

    [Fact]
    public async Task CheckCompleteness_Series_NoEpisodesInMetadata_ReturnsUnknown()
    {
        _episodeRepoMock.Setup(r => r.GetSeasonsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int>());

        var links = new List<Link>
        {
            CreateLink("https://example.com/S01E01.zip", season: 1, episode: 1)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Unknown);
    }

    // ── Episode type ─────────────────────────────────────────

    [Fact]
    public async Task CheckCompleteness_EpisodeType_ReturnsUnknown()
    {
        var links = new List<Link>
        {
            CreateLink("https://example.com/file.zip")
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Episode, links);

        result.Should().Be(CompletenessStatus.Unknown);
    }

    // ── Multi-season ─────────────────────────────────────────

    [Fact]
    public async Task CheckCompleteness_Series_MultiSeason_AllComplete_ReturnsComplete()
    {
        _episodeRepoMock.Setup(r => r.GetSeasonsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 1, 2 });
        _episodeRepoMock.Setup(r => r.GetEpisodeCountForSeasonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _episodeRepoMock.Setup(r => r.GetEpisodeCountForSeasonAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var links = new List<Link>
        {
            CreateLink("https://example.com/S01E01.zip", season: 1, episode: 1),
            CreateLink("https://example.com/S01E02.zip", season: 1, episode: 2),
            CreateLink("https://example.com/S02E01.zip", season: 2, episode: 1),
            CreateLink("https://example.com/S02E02.zip", season: 2, episode: 2)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Complete);
    }

    [Fact]
    public async Task CheckCompleteness_Series_MultiSeason_OneIncomplete_ReturnsIncomplete()
    {
        _episodeRepoMock.Setup(r => r.GetSeasonsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 1, 2 });
        _episodeRepoMock.Setup(r => r.GetEpisodeCountForSeasonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _episodeRepoMock.Setup(r => r.GetEpisodeCountForSeasonAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var links = new List<Link>
        {
            CreateLink("https://example.com/S01E01.zip", season: 1, episode: 1),
            CreateLink("https://example.com/S01E02.zip", season: 1, episode: 2),
            CreateLink("https://example.com/S02E01.zip", season: 2, episode: 1)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Incomplete);
    }

    [Fact]
    public async Task CheckCompleteness_Series_DuplicateEpisodeLinks_CountsDistinct()
    {
        _episodeRepoMock.Setup(r => r.GetSeasonsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 1 });
        _episodeRepoMock.Setup(r => r.GetEpisodeCountForSeasonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Two links for the same episode — should count as 1
        var links = new List<Link>
        {
            CreateLink("https://example.com/S01E01-720p.zip", season: 1, episode: 1),
            CreateLink("https://example.com/S01E01-1080p.zip", season: 1, episode: 1),
            CreateLink("https://example.com/S01E02.zip", season: 1, episode: 2)
        };

        var result = await _sut.CheckCompletenessAsync(1, MediaType.Series, links);

        result.Should().Be(CompletenessStatus.Complete);
    }

    // ── Helper ───────────────────────────────────────────────

    private static Link CreateLink(string url, int? season = null, int? episode = null)
    {
        return new Link
        {
            Url = url,
            PostUrl = "https://forum.example.com/post/1",
            LinkTypeId = 1,
            ParsedSeason = season,
            ParsedEpisode = episode,
            RunId = 1,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
