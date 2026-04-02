using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.Services.Imdb;

public class ImdbMetadataSourceTests
{
    private readonly IMediaTitleRepository _titleRepo = Substitute.For<IMediaTitleRepository>();
    private readonly IMediaEpisodeRepository _episodeRepo = Substitute.For<IMediaEpisodeRepository>();
    private readonly IMediaRatingRepository _ratingRepo = Substitute.For<IMediaRatingRepository>();
    private readonly IImdbTitleDetailsRepository _detailsRepo = Substitute.For<IImdbTitleDetailsRepository>();
    private readonly ITitleNormalizer _normalizer = Substitute.For<ITitleNormalizer>();
    private readonly ILogger<ImdbMetadataSource> _logger = Substitute.For<ILogger<ImdbMetadataSource>>();
    private readonly ImdbMetadataSource _sut;

    public ImdbMetadataSourceTests()
    {
        _sut = new ImdbMetadataSource(
            _titleRepo, _episodeRepo, _ratingRepo,
            _detailsRepo, _normalizer, _logger);
    }

    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullTitleRepo_Throws()
    {
        var act = () => new ImdbMetadataSource(
            null!, _episodeRepo, _ratingRepo,
            _detailsRepo, _normalizer, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("titleRepo");
    }

    [Fact]
    public void Constructor_NullEpisodeRepo_Throws()
    {
        var act = () => new ImdbMetadataSource(
            _titleRepo, null!, _ratingRepo,
            _detailsRepo, _normalizer, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("episodeRepo");
    }

    [Fact]
    public void Constructor_NullRatingRepo_Throws()
    {
        var act = () => new ImdbMetadataSource(
            _titleRepo, _episodeRepo, null!,
            _detailsRepo, _normalizer, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("ratingRepo");
    }

    [Fact]
    public void Constructor_NullDetailsRepo_Throws()
    {
        var act = () => new ImdbMetadataSource(
            _titleRepo, _episodeRepo, _ratingRepo,
            null!, _normalizer, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("imdbDetailsRepo");
    }

    [Fact]
    public void Constructor_NullNormalizer_Throws()
    {
        var act = () => new ImdbMetadataSource(
            _titleRepo, _episodeRepo, _ratingRepo,
            _detailsRepo, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("normalizer");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new ImdbMetadataSource(
            _titleRepo, _episodeRepo, _ratingRepo,
            _detailsRepo, _normalizer, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ── SourceIdentifier ───────────────────────────────────────────

    [Fact]
    public void SourceIdentifier_ReturnsImdb()
    {
        _sut.SourceIdentifier.Should().Be("imdb");
    }

    // ── SearchByTitleAsync ─────────────────────────────────────────

    [Fact]
    public async Task SearchByTitleAsync_NullQuery_ReturnsEmpty()
    {
        var result = await _sut.SearchByTitleAsync(null!);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_EmptyQuery_ReturnsEmpty()
    {
        var result = await _sut.SearchByTitleAsync("");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_NormalizesToEmpty_ReturnsEmpty()
    {
        _normalizer.Normalize("!!!").Returns("");

        var result = await _sut.SearchByTitleAsync("!!!");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_ExactCanonicalMatch_ReturnsHighConfidence()
    {
        SetupNormalizer();

        var title = CreateMediaTitle(1, "The Matrix", 1999, MediaType.Movie);
        _titleRepo.SearchByTitleAsync("the matrix", null, null, ImdbMetadataSource.MaxCandidates, Arg.Any<CancellationToken>())
            .Returns(new[] { title });
        _titleRepo.SearchByAliasAsync("the matrix", null, null, ImdbMetadataSource.MaxCandidates, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitleAlias>());
        _detailsRepo.GetByMediaIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, ImdbTitleDetails>
            {
                [1] = new() { MediaId = 1, Tconst = "tt0133093" }
            });

        _normalizer.ComputeSimilarity("The Matrix", "The Matrix").Returns(1.0m);

        var result = await _sut.SearchByTitleAsync("The Matrix");

        result.Should().HaveCount(1);
        result[0].CanonicalTitle.Should().Be("The Matrix");
        result[0].ConfidenceScore.Should().Be(1.0m);
        result[0].ExternalId.Should().Be("tt0133093");
        result[0].MediaId.Should().Be(1);
    }

    [Fact]
    public async Task SearchByTitleAsync_AliasMatch_ReturnsWithPenalty()
    {
        SetupNormalizer();

        var title = CreateMediaTitle(1, "The Matrix", 1999, MediaType.Movie);
        var alias = new MediaTitleAlias { AliasId = 1, MediaId = 1, AliasTitle = "Matrix", MediaTitle = title };

        _titleRepo.SearchByTitleAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitle>());
        _titleRepo.SearchByAliasAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { alias });
        _detailsRepo.GetByMediaIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, ImdbTitleDetails>
            {
                [1] = new() { MediaId = 1, Tconst = "tt0133093" }
            });

        _normalizer.ComputeSimilarity("Matrix", "Matrix").Returns(1.0m);

        var result = await _sut.SearchByTitleAsync("Matrix");

        result.Should().HaveCount(1);
        result[0].ConfidenceScore.Should().Be(1.0m - ImdbMetadataSource.AliasPenalty);
    }

    [Fact]
    public async Task SearchByTitleAsync_WithYearFilter_AppliesYearBonus()
    {
        SetupNormalizer();

        var title = CreateMediaTitle(1, "The Matrix", 1999, MediaType.Movie);
        _titleRepo.SearchByTitleAsync("the matrix", null, 1999, ImdbMetadataSource.MaxCandidates, Arg.Any<CancellationToken>())
            .Returns(new[] { title });
        _titleRepo.SearchByAliasAsync("the matrix", null, 1999, ImdbMetadataSource.MaxCandidates, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitleAlias>());
        _detailsRepo.GetByMediaIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, ImdbTitleDetails>
            {
                [1] = new() { MediaId = 1, Tconst = "tt0133093" }
            });

        _normalizer.ComputeSimilarity("The Matrix", "The Matrix").Returns(0.9m);

        var result = await _sut.SearchByTitleAsync("The Matrix", year: 1999);

        result.Should().HaveCount(1);
        result[0].ConfidenceScore.Should().Be(0.9m + ImdbMetadataSource.YearBonus);
    }

    [Fact]
    public async Task SearchByTitleAsync_WithTypeFilter_PassesToRepository()
    {
        SetupNormalizer();

        _titleRepo.SearchByTitleAsync("the matrix", MediaType.Movie, null, ImdbMetadataSource.MaxCandidates, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitle>());
        _titleRepo.SearchByAliasAsync("the matrix", MediaType.Movie, null, ImdbMetadataSource.MaxCandidates, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitleAlias>());

        await _sut.SearchByTitleAsync("The Matrix", type: "movie");

        await _titleRepo.Received(1).SearchByTitleAsync("the matrix", MediaType.Movie, null, ImdbMetadataSource.MaxCandidates, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchByTitleAsync_MultipleMatches_RankedByConfidence()
    {
        SetupNormalizer();

        var title1 = CreateMediaTitle(1, "Breaking Bad", 2008, MediaType.Series);
        var title2 = CreateMediaTitle(2, "Breaking Badly", 2010, MediaType.Movie);

        _titleRepo.SearchByTitleAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { title1, title2 });
        _titleRepo.SearchByAliasAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitleAlias>());
        _detailsRepo.GetByMediaIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, ImdbTitleDetails>
            {
                [1] = new() { MediaId = 1, Tconst = "tt0903747" },
                [2] = new() { MediaId = 2, Tconst = "tt9999999" }
            });

        _normalizer.ComputeSimilarity("Breaking Bad", "Breaking Bad").Returns(1.0m);
        _normalizer.ComputeSimilarity("Breaking Bad", "Breaking Badly").Returns(0.85m);

        var result = await _sut.SearchByTitleAsync("Breaking Bad");

        result.Should().HaveCount(2);
        result[0].ConfidenceScore.Should().BeGreaterThan(result[1].ConfidenceScore);
        result[0].ExternalId.Should().Be("tt0903747");
    }

    [Fact]
    public async Task SearchByTitleAsync_NoImdbDetails_SkipsTitle()
    {
        SetupNormalizer();

        var title = CreateMediaTitle(1, "NoDetails Movie", 2020, MediaType.Movie);
        _titleRepo.SearchByTitleAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { title });
        _titleRepo.SearchByAliasAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitleAlias>());
        _detailsRepo.GetByMediaIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, ImdbTitleDetails>());

        var result = await _sut.SearchByTitleAsync("NoDetails Movie");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_DuplicateFromCanonicalAndAlias_KeepsBestScore()
    {
        SetupNormalizer();

        var title = CreateMediaTitle(1, "Inception", 2010, MediaType.Movie);
        var alias = new MediaTitleAlias { AliasId = 1, MediaId = 1, AliasTitle = "Inception", MediaTitle = title };

        _titleRepo.SearchByTitleAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { title });
        _titleRepo.SearchByAliasAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { alias });
        _detailsRepo.GetByMediaIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, ImdbTitleDetails>
            {
                [1] = new() { MediaId = 1, Tconst = "tt1375666" }
            });

        _normalizer.ComputeSimilarity("Inception", "Inception").Returns(1.0m);

        var result = await _sut.SearchByTitleAsync("Inception");

        result.Should().HaveCount(1);
        result[0].ConfidenceScore.Should().Be(1.0m);
    }

    [Fact]
    public async Task SearchByTitleAsync_NoCandidatesFromEither_ReturnsEmpty()
    {
        SetupNormalizer();

        _titleRepo.SearchByTitleAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitle>());
        _titleRepo.SearchByAliasAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitleAlias>());

        var result = await _sut.SearchByTitleAsync("Nonexistent");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_YearBonusCappedAt1()
    {
        SetupNormalizer();

        var title = CreateMediaTitle(1, "Movie", 2020, MediaType.Movie);
        _titleRepo.SearchByTitleAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { title });
        _titleRepo.SearchByAliasAsync(Arg.Any<string>(), Arg.Any<MediaType?>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaTitleAlias>());
        _detailsRepo.GetByMediaIdsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, ImdbTitleDetails>
            {
                [1] = new() { MediaId = 1, Tconst = "tt1111111" }
            });

        _normalizer.ComputeSimilarity("Movie", "Movie").Returns(0.98m);

        var result = await _sut.SearchByTitleAsync("Movie", year: 2020);

        result.Should().HaveCount(1);
        result[0].ConfidenceScore.Should().Be(1.0m);
    }

    // ── SearchByExternalIdAsync ────────────────────────────────────

    [Fact]
    public async Task SearchByExternalIdAsync_NullId_ReturnsNull()
    {
        var result = await _sut.SearchByExternalIdAsync(null!);
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchByExternalIdAsync_EmptyId_ReturnsNull()
    {
        var result = await _sut.SearchByExternalIdAsync("");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchByExternalIdAsync_ValidTconst_ReturnsResult()
    {
        var details = new ImdbTitleDetails { MediaId = 1, Tconst = "tt0133093" };
        var title = CreateMediaTitle(1, "The Matrix", 1999, MediaType.Movie);

        _detailsRepo.GetByTconstAsync("tt0133093", Arg.Any<CancellationToken>()).Returns(details);
        _titleRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(title);

        var result = await _sut.SearchByExternalIdAsync("tt0133093");

        result.Should().NotBeNull();
        result!.CanonicalTitle.Should().Be("The Matrix");
        result.Year.Should().Be(1999);
        result.Type.Should().Be("movie");
        result.ConfidenceScore.Should().Be(1.0m);
        result.ExternalId.Should().Be("tt0133093");
    }

    [Fact]
    public async Task SearchByExternalIdAsync_TconstNotFound_ReturnsNull()
    {
        _detailsRepo.GetByTconstAsync("tt0000000", Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.SearchByExternalIdAsync("tt0000000");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchByExternalIdAsync_TitleNotFound_ReturnsNull()
    {
        var details = new ImdbTitleDetails { MediaId = 999, Tconst = "tt0000001" };
        _detailsRepo.GetByTconstAsync("tt0000001", Arg.Any<CancellationToken>()).Returns(details);
        _titleRepo.GetByIdAsync(999, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.SearchByExternalIdAsync("tt0000001");

        result.Should().BeNull();
    }

    // ── GetEpisodeListAsync ────────────────────────────────────────

    [Fact]
    public async Task GetEpisodeListAsync_ReturnsEpisodes()
    {
        var episodes = new List<MediaEpisode>
        {
            new() { EpisodeId = 1, MediaId = 1, Season = 1, EpisodeNumber = 1 },
            new() { EpisodeId = 2, MediaId = 1, Season = 1, EpisodeNumber = 2 },
            new() { EpisodeId = 3, MediaId = 1, Season = 2, EpisodeNumber = 1 }
        };

        _episodeRepo.GetByMediaIdAsync(1, Arg.Any<CancellationToken>()).Returns(episodes);

        var result = await _sut.GetEpisodeListAsync(1);

        result.Should().HaveCount(3);
        result[0].Season.Should().Be(1);
        result[0].EpisodeNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetEpisodeListAsync_WithSeasonFilter_FiltersCorrectly()
    {
        var episodes = new List<MediaEpisode>
        {
            new() { EpisodeId = 1, MediaId = 1, Season = 1, EpisodeNumber = 1 },
            new() { EpisodeId = 2, MediaId = 1, Season = 1, EpisodeNumber = 2 },
            new() { EpisodeId = 3, MediaId = 1, Season = 2, EpisodeNumber = 1 }
        };

        _episodeRepo.GetByMediaIdAsync(1, Arg.Any<CancellationToken>()).Returns(episodes);

        var result = await _sut.GetEpisodeListAsync(1, season: 1);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.Season == 1);
    }

    [Fact]
    public async Task GetEpisodeListAsync_NoEpisodes_ReturnsEmpty()
    {
        _episodeRepo.GetByMediaIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaEpisode>());

        var result = await _sut.GetEpisodeListAsync(1);

        result.Should().BeEmpty();
    }

    // ── GetRatingsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetRatingsAsync_RatingExists_ReturnsRatingInfo()
    {
        var rating = new MediaRating { MediaId = 1, SourceId = 1, Rating = 8.7m, VoteCount = 2000000 };
        _ratingRepo.GetByMediaIdAndSourceAsync(1, ImdbMetadataSource.ImdbSourceId, Arg.Any<CancellationToken>())
            .Returns(rating);

        var result = await _sut.GetRatingsAsync(1);

        result.Should().NotBeNull();
        result!.Rating.Should().Be(8.7m);
        result.VoteCount.Should().Be(2000000);
    }

    [Fact]
    public async Task GetRatingsAsync_NoRating_ReturnsNull()
    {
        _ratingRepo.GetByMediaIdAndSourceAsync(1, ImdbMetadataSource.ImdbSourceId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.GetRatingsAsync(1);

        result.Should().BeNull();
    }

    // ── ParseMediaType ─────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("movie", MediaType.Movie)]
    [InlineData("Movie", MediaType.Movie)]
    [InlineData("MOVIE", MediaType.Movie)]
    [InlineData("series", MediaType.Series)]
    [InlineData("episode", MediaType.Episode)]
    [InlineData("unknown", null)]
    public void ParseMediaType_ReturnsExpected(string? input, MediaType? expected)
    {
        ImdbMetadataSource.ParseMediaType(input).Should().Be(expected);
    }

    // ── MediaTypeToString ──────────────────────────────────────────

    [Theory]
    [InlineData(MediaType.Movie, "movie")]
    [InlineData(MediaType.Series, "series")]
    [InlineData(MediaType.Episode, "episode")]
    public void MediaTypeToString_ReturnsExpected(MediaType input, string expected)
    {
        ImdbMetadataSource.MediaTypeToString(input).Should().Be(expected);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void SetupNormalizer()
    {
        _normalizer.Normalize(Arg.Any<string>())
            .Returns(callInfo => callInfo.Arg<string>()?.ToLowerInvariant() ?? "");
    }

    private static MediaTitle CreateMediaTitle(int id, string title, int? year, MediaType type)
    {
        return new MediaTitle
        {
            MediaId = id,
            CanonicalTitle = title,
            Year = year,
            Type = type,
            SourceId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
