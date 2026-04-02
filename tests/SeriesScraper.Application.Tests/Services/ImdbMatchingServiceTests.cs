using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Tests.Services;

public class ImdbMatchingServiceTests
{
    private readonly Mock<IMetadataSource> _metadataSourceMock = new();
    private readonly Mock<ITitleNormalizer> _normalizerMock = new();
    private readonly Mock<ILogger<ImdbMatchingService>> _loggerMock = new();
    private readonly ImdbMatchingService _sut;

    public ImdbMatchingServiceTests()
    {
        _sut = new ImdbMatchingService(
            _metadataSourceMock.Object,
            _normalizerMock.Object,
            _loggerMock.Object);
    }

    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullMetadataSource_ThrowsArgumentNullException()
    {
        var act = () => new ImdbMatchingService(null!, _normalizerMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("metadataSource");
    }

    [Fact]
    public void Constructor_NullNormalizer_ThrowsArgumentNullException()
    {
        var act = () => new ImdbMatchingService(_metadataSourceMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("normalizer");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new ImdbMatchingService(_metadataSourceMock.Object, _normalizerMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ── FindMatchesAsync ───────────────────────────────────────────

    [Fact]
    public async Task FindMatchesAsync_NullTitle_ReturnsEmpty()
    {
        var result = await _sut.FindMatchesAsync(null!);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatchesAsync_EmptyTitle_ReturnsEmpty()
    {
        var result = await _sut.FindMatchesAsync("");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatchesAsync_WhitespaceTitle_ReturnsEmpty()
    {
        var result = await _sut.FindMatchesAsync("   ");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatchesAsync_TitleWithYear_ExtractsYearAndSearches()
    {
        _normalizerMock.Setup(n => n.ExtractYear("Breaking Bad (2008)")).Returns(2008);
        _normalizerMock.Setup(n => n.StripYear("Breaking Bad (2008)")).Returns("Breaking Bad");

        var expected = new List<MetadataSearchResult>
        {
            new() { CanonicalTitle = "Breaking Bad", Year = 2008, Type = "series", ConfidenceScore = 0.95m, ExternalId = "tt0903747" }
        };

        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync("Breaking Bad", 2008, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.FindMatchesAsync("Breaking Bad (2008)");

        result.Should().BeEquivalentTo(expected);
        _metadataSourceMock.Verify(m => m.SearchByTitleAsync("Breaking Bad", 2008, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindMatchesAsync_TitleWithoutYear_SearchesWithNullYear()
    {
        _normalizerMock.Setup(n => n.ExtractYear("Breaking Bad")).Returns((int?)null);
        _normalizerMock.Setup(n => n.StripYear("Breaking Bad")).Returns("Breaking Bad");

        var expected = new List<MetadataSearchResult>
        {
            new() { CanonicalTitle = "Breaking Bad", Year = 2008, Type = "series", ConfidenceScore = 0.9m, ExternalId = "tt0903747" }
        };

        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync("Breaking Bad", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.FindMatchesAsync("Breaking Bad");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task FindMatchesAsync_WithType_PassesTypeToSource()
    {
        _normalizerMock.Setup(n => n.ExtractYear("The Matrix")).Returns((int?)null);
        _normalizerMock.Setup(n => n.StripYear("The Matrix")).Returns("The Matrix");
        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync("The Matrix", null, "movie", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MetadataSearchResult>());

        await _sut.FindMatchesAsync("The Matrix", "movie");

        _metadataSourceMock.Verify(
            m => m.SearchByTitleAsync("The Matrix", null, "movie", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindMatchesAsync_NoMatchesFound_ReturnsEmpty()
    {
        _normalizerMock.Setup(n => n.ExtractYear(It.IsAny<string>())).Returns((int?)null);
        _normalizerMock.Setup(n => n.StripYear(It.IsAny<string>())).Returns("NonexistentMovie");
        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MetadataSearchResult>());

        var result = await _sut.FindMatchesAsync("NonexistentMovie");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatchesAsync_StripYearReturnsEmpty_ReturnsEmpty()
    {
        _normalizerMock.Setup(n => n.ExtractYear("(2024)")).Returns(2024);
        _normalizerMock.Setup(n => n.StripYear("(2024)")).Returns("");

        var result = await _sut.FindMatchesAsync("(2024)");

        result.Should().BeEmpty();
    }

    // ── FindBestMatchAsync ─────────────────────────────────────────

    [Fact]
    public async Task FindBestMatchAsync_NullTitle_ReturnsNull()
    {
        var result = await _sut.FindBestMatchAsync(null!);
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindBestMatchAsync_HighConfidenceMatch_ReturnsIt()
    {
        _normalizerMock.Setup(n => n.ExtractYear("Breaking Bad")).Returns((int?)null);
        _normalizerMock.Setup(n => n.StripYear("Breaking Bad")).Returns("Breaking Bad");

        var results = new List<MetadataSearchResult>
        {
            new() { CanonicalTitle = "Breaking Bad", Year = 2008, Type = "series", ConfidenceScore = 0.95m, ExternalId = "tt0903747" },
            new() { CanonicalTitle = "Breaking Badly", Year = 2010, Type = "movie", ConfidenceScore = 0.6m, ExternalId = "tt9999999" }
        };

        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var result = await _sut.FindBestMatchAsync("Breaking Bad");

        result.Should().NotBeNull();
        result!.ExternalId.Should().Be("tt0903747");
        result.ConfidenceScore.Should().Be(0.95m);
    }

    [Fact]
    public async Task FindBestMatchAsync_BelowThreshold_ReturnsNull()
    {
        _normalizerMock.Setup(n => n.ExtractYear("Some Movie")).Returns((int?)null);
        _normalizerMock.Setup(n => n.StripYear("Some Movie")).Returns("Some Movie");

        var results = new List<MetadataSearchResult>
        {
            new() { CanonicalTitle = "Different Movie", Year = 2020, Type = "movie", ConfidenceScore = 0.3m, ExternalId = "tt1111111" }
        };

        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var result = await _sut.FindBestMatchAsync("Some Movie");

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindBestMatchAsync_ExactThreshold_ReturnsMatch()
    {
        _normalizerMock.Setup(n => n.ExtractYear("Movie")).Returns((int?)null);
        _normalizerMock.Setup(n => n.StripYear("Movie")).Returns("Movie");

        var results = new List<MetadataSearchResult>
        {
            new() { CanonicalTitle = "Movie", Year = 2020, Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1234567" }
        };

        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var result = await _sut.FindBestMatchAsync("Movie");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FindBestMatchAsync_EmptyResults_ReturnsNull()
    {
        _normalizerMock.Setup(n => n.ExtractYear("Unknown")).Returns((int?)null);
        _normalizerMock.Setup(n => n.StripYear("Unknown")).Returns("Unknown");
        _metadataSourceMock
            .Setup(m => m.SearchByTitleAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MetadataSearchResult>());

        var result = await _sut.FindBestMatchAsync("Unknown");

        result.Should().BeNull();
    }
}
