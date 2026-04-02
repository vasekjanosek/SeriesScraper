using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class MediaRatingRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly MediaRatingRepository _sut;

    public MediaRatingRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);

        _context.DataSources.Add(new DataSource { SourceId = 1, Name = "IMDB" });
        _context.DataSources.Add(new DataSource { SourceId = 2, Name = "CSFD" });
        _context.SaveChanges();

        _sut = new MediaRatingRepository(_context);
    }

    [Fact]
    public async Task GetByMediaIdAndSourceAsync_Exists_ReturnsRating()
    {
        SeedRating(1, 1, 8.7m, 2000000);

        var result = await _sut.GetByMediaIdAndSourceAsync(1, 1);

        result.Should().NotBeNull();
        result!.Rating.Should().Be(8.7m);
        result.VoteCount.Should().Be(2000000);
    }

    [Fact]
    public async Task GetByMediaIdAndSourceAsync_WrongSource_ReturnsNull()
    {
        SeedRating(1, 1, 8.7m, 2000000);

        var result = await _sut.GetByMediaIdAndSourceAsync(1, 2);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByMediaIdAndSourceAsync_WrongMediaId_ReturnsNull()
    {
        SeedRating(1, 1, 8.7m, 2000000);

        var result = await _sut.GetByMediaIdAndSourceAsync(999, 1);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByMediaIdAndSourceAsync_MultipleRatings_ReturnsCorrectOne()
    {
        SeedRating(1, 1, 8.7m, 2000000);
        SeedRating(1, 2, 85.0m, 50000);

        var imdbRating = await _sut.GetByMediaIdAndSourceAsync(1, 1);
        var csfdRating = await _sut.GetByMediaIdAndSourceAsync(1, 2);

        imdbRating!.Rating.Should().Be(8.7m);
        csfdRating!.Rating.Should().Be(85.0m);
    }

    [Fact]
    public async Task GetByMediaIdAndSourceAsync_NoRatings_ReturnsNull()
    {
        var result = await _sut.GetByMediaIdAndSourceAsync(1, 1);
        result.Should().BeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void SeedRating(int mediaId, int sourceId, decimal rating, int voteCount)
    {
        // Ensure media title exists
        if (!_context.MediaTitles.Any(t => t.MediaId == mediaId))
        {
            _context.MediaTitles.Add(new MediaTitle
            {
                MediaId = mediaId,
                CanonicalTitle = $"Title {mediaId}",
                Year = 2020,
                Type = Domain.Enums.MediaType.Movie,
                SourceId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        _context.MediaRatings.Add(new MediaRating
        {
            MediaId = mediaId,
            SourceId = sourceId,
            Rating = rating,
            VoteCount = voteCount
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
