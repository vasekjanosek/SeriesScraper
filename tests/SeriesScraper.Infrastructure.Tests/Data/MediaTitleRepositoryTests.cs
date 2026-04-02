using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class MediaTitleRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly MediaTitleRepository _sut;

    public MediaTitleRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        _context.DataSources.Add(new DataSource { SourceId = 1, Name = "IMDB" });
        _context.SaveChanges();

        _sut = new MediaTitleRepository(_context);
    }

    // ── SearchByTitleAsync ─────────────────────────────────────────

    [Fact]
    public async Task SearchByTitleAsync_EmptyTerm_ReturnsEmpty()
    {
        var result = await _sut.SearchByTitleAsync("");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_NullTerm_ReturnsEmpty()
    {
        var result = await _sut.SearchByTitleAsync(null!);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_ExactMatch_ReturnsTitle()
    {
        SeedTitle(1, "The Matrix", 1999, MediaType.Movie);

        var result = await _sut.SearchByTitleAsync("the matrix");

        result.Should().HaveCount(1);
        result[0].CanonicalTitle.Should().Be("The Matrix");
    }

    [Fact]
    public async Task SearchByTitleAsync_PartialMatch_ReturnsTitle()
    {
        SeedTitle(1, "The Matrix", 1999, MediaType.Movie);

        var result = await _sut.SearchByTitleAsync("matrix");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchByTitleAsync_CaseInsensitive_ReturnsTitle()
    {
        SeedTitle(1, "Breaking Bad", 2008, MediaType.Series);

        var result = await _sut.SearchByTitleAsync("breaking bad");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchByTitleAsync_NoMatch_ReturnsEmpty()
    {
        SeedTitle(1, "The Matrix", 1999, MediaType.Movie);

        var result = await _sut.SearchByTitleAsync("inception");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_TypeFilter_FiltersCorrectly()
    {
        SeedTitle(1, "Breaking Bad", 2008, MediaType.Series);
        SeedTitle(2, "Breaking Bad Movie", 2020, MediaType.Movie);

        var result = await _sut.SearchByTitleAsync("breaking bad", MediaType.Series);

        result.Should().HaveCount(1);
        result[0].Type.Should().Be(MediaType.Series);
    }

    [Fact]
    public async Task SearchByTitleAsync_YearFilter_FiltersCorrectly()
    {
        SeedTitle(1, "The Matrix", 1999, MediaType.Movie);
        SeedTitle(2, "The Matrix Resurrections", 2021, MediaType.Movie);

        var result = await _sut.SearchByTitleAsync("the matrix", year: 1999);

        result.Should().HaveCount(1);
        result[0].Year.Should().Be(1999);
    }

    [Fact]
    public async Task SearchByTitleAsync_MaxResults_LimitsResults()
    {
        for (int i = 1; i <= 5; i++)
            SeedTitle(i, $"Test Movie {i}", 2020, MediaType.Movie);

        var result = await _sut.SearchByTitleAsync("test movie", maxResults: 3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchByTitleAsync_MultipleMatches_OrderedByTitle()
    {
        SeedTitle(1, "Breaking Bad", 2008, MediaType.Series);
        SeedTitle(2, "Better Call Saul: Breaking Bad Crossover", 2022, MediaType.Movie);

        var result = await _sut.SearchByTitleAsync("breaking bad");

        result.Should().HaveCount(2);
        string.Compare(result[0].CanonicalTitle, result[1].CanonicalTitle, StringComparison.Ordinal)
            .Should().BeLessThan(0);
    }

    // ── SearchByAliasAsync ─────────────────────────────────────────

    [Fact]
    public async Task SearchByAliasAsync_EmptyTerm_ReturnsEmpty()
    {
        var result = await _sut.SearchByAliasAsync("");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByAliasAsync_MatchesAlias_ReturnsAliasWithTitle()
    {
        SeedTitle(1, "The Matrix", 1999, MediaType.Movie);
        SeedAlias(1, 1, "Matrix");

        var result = await _sut.SearchByAliasAsync("matrix");

        result.Should().HaveCount(1);
        result[0].AliasTitle.Should().Be("Matrix");
        result[0].MediaTitle.Should().NotBeNull();
        result[0].MediaTitle.CanonicalTitle.Should().Be("The Matrix");
    }

    [Fact]
    public async Task SearchByAliasAsync_CaseInsensitive_MatchesAlias()
    {
        SeedTitle(1, "Amélie", 2001, MediaType.Movie);
        SeedAlias(1, 1, "Amelie");

        var result = await _sut.SearchByAliasAsync("amelie");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchByAliasAsync_TypeFilter_FiltersViaTitleType()
    {
        SeedTitle(1, "Breaking Bad", 2008, MediaType.Series);
        SeedTitle(2, "Breaking", 2020, MediaType.Movie);
        SeedAlias(1, 1, "BB");
        SeedAlias(2, 2, "BR");

        var result = await _sut.SearchByAliasAsync("b", MediaType.Series);

        result.Should().HaveCount(1);
        result[0].MediaTitle.Type.Should().Be(MediaType.Series);
    }

    [Fact]
    public async Task SearchByAliasAsync_YearFilter_FiltersViaTitleYear()
    {
        SeedTitle(1, "Movie A", 2010, MediaType.Movie);
        SeedTitle(2, "Movie B", 2020, MediaType.Movie);
        SeedAlias(1, 1, "Film A");
        SeedAlias(2, 2, "Film B");

        var result = await _sut.SearchByAliasAsync("film", year: 2020);

        result.Should().HaveCount(1);
        result[0].AliasTitle.Should().Be("Film B");
    }

    [Fact]
    public async Task SearchByAliasAsync_NoMatch_ReturnsEmpty()
    {
        SeedTitle(1, "The Matrix", 1999, MediaType.Movie);
        SeedAlias(1, 1, "Matrix");

        var result = await _sut.SearchByAliasAsync("inception");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByAliasAsync_MaxResults_LimitsResults()
    {
        SeedTitle(1, "Movie", 2020, MediaType.Movie);
        for (int i = 1; i <= 5; i++)
            SeedAlias(i, 1, $"Alias {i}");

        var result = await _sut.SearchByAliasAsync("alias", maxResults: 3);

        result.Should().HaveCount(3);
    }

    // ── GetByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_Exists_ReturnsTitle()
    {
        SeedTitle(1, "The Matrix", 1999, MediaType.Movie);

        var result = await _sut.GetByIdAsync(1);

        result.Should().NotBeNull();
        result!.CanonicalTitle.Should().Be("The Matrix");
    }

    [Fact]
    public async Task GetByIdAsync_NotExists_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(999);
        result.Should().BeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void SeedTitle(int id, string title, int? year, MediaType type)
    {
        _context.MediaTitles.Add(new MediaTitle
        {
            MediaId = id,
            CanonicalTitle = title,
            Year = year,
            Type = type,
            SourceId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    private void SeedAlias(int aliasId, int mediaId, string aliasTitle)
    {
        _context.MediaTitleAliases.Add(new MediaTitleAlias
        {
            AliasId = aliasId,
            MediaId = mediaId,
            AliasTitle = aliasTitle
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
