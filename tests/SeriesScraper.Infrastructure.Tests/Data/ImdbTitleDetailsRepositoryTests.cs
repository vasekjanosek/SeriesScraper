using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class ImdbTitleDetailsRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ImdbTitleDetailsRepository _sut;

    public ImdbTitleDetailsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        _context.DataSources.Add(new DataSource { SourceId = 1, Name = "IMDB" });
        _context.SaveChanges();

        _sut = new ImdbTitleDetailsRepository(_context);
    }

    // ── GetByTconstAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetByTconstAsync_NullTconst_ReturnsNull()
    {
        var result = await _sut.GetByTconstAsync(null!);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTconstAsync_EmptyTconst_ReturnsNull()
    {
        var result = await _sut.GetByTconstAsync("");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTconstAsync_Exists_ReturnsDetails()
    {
        SeedTitleWithDetails(1, "The Matrix", "tt0133093", "Action,Sci-Fi");

        var result = await _sut.GetByTconstAsync("tt0133093");

        result.Should().NotBeNull();
        result!.Tconst.Should().Be("tt0133093");
        result.MediaId.Should().Be(1);
        result.GenreString.Should().Be("Action,Sci-Fi");
    }

    [Fact]
    public async Task GetByTconstAsync_NotExists_ReturnsNull()
    {
        var result = await _sut.GetByTconstAsync("tt0000000");
        result.Should().BeNull();
    }

    // ── GetByMediaIdAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetByMediaIdAsync_Exists_ReturnsDetails()
    {
        SeedTitleWithDetails(1, "The Matrix", "tt0133093");

        var result = await _sut.GetByMediaIdAsync(1);

        result.Should().NotBeNull();
        result!.Tconst.Should().Be("tt0133093");
    }

    [Fact]
    public async Task GetByMediaIdAsync_NotExists_ReturnsNull()
    {
        var result = await _sut.GetByMediaIdAsync(999);
        result.Should().BeNull();
    }

    // ── GetByMediaIdsAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetByMediaIdsAsync_EmptyList_ReturnsEmptyDictionary()
    {
        var result = await _sut.GetByMediaIdsAsync(Array.Empty<int>());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByMediaIdsAsync_AllExist_ReturnsDictionary()
    {
        SeedTitleWithDetails(1, "The Matrix", "tt0133093");
        SeedTitleWithDetails(2, "Inception", "tt1375666");

        var result = await _sut.GetByMediaIdsAsync(new[] { 1, 2 });

        result.Should().HaveCount(2);
        result[1].Tconst.Should().Be("tt0133093");
        result[2].Tconst.Should().Be("tt1375666");
    }

    [Fact]
    public async Task GetByMediaIdsAsync_SomeExist_ReturnsOnlyExisting()
    {
        SeedTitleWithDetails(1, "The Matrix", "tt0133093");

        var result = await _sut.GetByMediaIdsAsync(new[] { 1, 999 });

        result.Should().HaveCount(1);
        result.Should().ContainKey(1);
        result.Should().NotContainKey(999);
    }

    [Fact]
    public async Task GetByMediaIdsAsync_NoneExist_ReturnsEmptyDictionary()
    {
        var result = await _sut.GetByMediaIdsAsync(new[] { 998, 999 });
        result.Should().BeEmpty();
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void SeedTitleWithDetails(int mediaId, string title, string tconst, string? genres = null)
    {
        _context.MediaTitles.Add(new MediaTitle
        {
            MediaId = mediaId,
            CanonicalTitle = title,
            Year = 1999,
            Type = MediaType.Movie,
            SourceId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        _context.ImdbTitleDetails.Add(new ImdbTitleDetails
        {
            MediaId = mediaId,
            Tconst = tconst,
            GenreString = genres
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
