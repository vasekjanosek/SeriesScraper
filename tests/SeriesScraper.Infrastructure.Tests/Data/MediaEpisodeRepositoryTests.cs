using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class MediaEpisodeRepositoryTests
{
    private static (AppDbContext Context, MediaEpisodeRepository Repository) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return (context, new MediaEpisodeRepository(context));
    }

    private static async Task<MediaTitle> SeedMediaTitle(AppDbContext context)
    {
        // DataSource is seeded by EnsureCreated (seed data: SourceId=1, Name="IMDB")
        // Use the seed SourceId directly
        var sourceId = 1;

        var title = new MediaTitle
        {
            CanonicalTitle = "Test Series",
            Year = 2024,
            Type = MediaType.Series,
            SourceId = sourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.Add(title);
        await context.SaveChangesAsync();

        return title;
    }

    // ── GetByMediaIdAsync ────────────────────────────────────

    [Fact]
    public async Task GetByMediaIdAsync_ReturnsEpisodesOrderedBySeasonAndNumber()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var title = await SeedMediaTitle(context);

        context.MediaEpisodes.AddRange(
            new MediaEpisode { MediaId = title.MediaId, Season = 1, EpisodeNumber = 2 },
            new MediaEpisode { MediaId = title.MediaId, Season = 1, EpisodeNumber = 1 },
            new MediaEpisode { MediaId = title.MediaId, Season = 2, EpisodeNumber = 1 }
        );
        await context.SaveChangesAsync();

        var result = await sut.GetByMediaIdAsync(title.MediaId);

        result.Should().HaveCount(3);
        result[0].Season.Should().Be(1);
        result[0].EpisodeNumber.Should().Be(1);
        result[1].Season.Should().Be(1);
        result[1].EpisodeNumber.Should().Be(2);
        result[2].Season.Should().Be(2);
    }

    [Fact]
    public async Task GetByMediaIdAsync_NoEpisodes_ReturnsEmpty()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var title = await SeedMediaTitle(context);

        var result = await sut.GetByMediaIdAsync(title.MediaId);

        result.Should().BeEmpty();
    }

    // ── GetEpisodeCountForSeasonAsync ────────────────────────

    [Fact]
    public async Task GetEpisodeCountForSeasonAsync_ReturnsCorrectCount()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var title = await SeedMediaTitle(context);

        context.MediaEpisodes.AddRange(
            new MediaEpisode { MediaId = title.MediaId, Season = 1, EpisodeNumber = 1 },
            new MediaEpisode { MediaId = title.MediaId, Season = 1, EpisodeNumber = 2 },
            new MediaEpisode { MediaId = title.MediaId, Season = 2, EpisodeNumber = 1 }
        );
        await context.SaveChangesAsync();

        var count = await sut.GetEpisodeCountForSeasonAsync(title.MediaId, 1);

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetEpisodeCountForSeasonAsync_NoEpisodes_ReturnsZero()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var title = await SeedMediaTitle(context);

        var count = await sut.GetEpisodeCountForSeasonAsync(title.MediaId, 1);

        count.Should().Be(0);
    }

    // ── GetSeasonsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetSeasonsAsync_ReturnsDistinctSeasonsOrdered()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var title = await SeedMediaTitle(context);

        context.MediaEpisodes.AddRange(
            new MediaEpisode { MediaId = title.MediaId, Season = 3, EpisodeNumber = 1 },
            new MediaEpisode { MediaId = title.MediaId, Season = 1, EpisodeNumber = 1 },
            new MediaEpisode { MediaId = title.MediaId, Season = 1, EpisodeNumber = 2 },
            new MediaEpisode { MediaId = title.MediaId, Season = 2, EpisodeNumber = 1 }
        );
        await context.SaveChangesAsync();

        var result = await sut.GetSeasonsAsync(title.MediaId);

        result.Should().BeEquivalentTo(new[] { 1, 2, 3 }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetSeasonsAsync_NoEpisodes_ReturnsEmpty()
    {
        var (context, sut) = CreateSut();
        using var _ = context;
        var title = await SeedMediaTitle(context);

        var result = await sut.GetSeasonsAsync(title.MediaId);

        result.Should().BeEmpty();
    }
}
