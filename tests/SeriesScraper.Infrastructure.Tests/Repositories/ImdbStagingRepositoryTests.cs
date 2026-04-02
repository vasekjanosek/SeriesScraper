using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class ImdbStagingRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public ImdbStagingRepositoryTests(PostgresqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await CleanupAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task CleanupAsync()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(@"
            DELETE FROM media_title_aliases;
            DELETE FROM media_episodes;
            DELETE FROM media_ratings;
            DELETE FROM imdb_title_details;
            DELETE FROM media_titles;
            TRUNCATE TABLE imdb_title_basics_staging;
            TRUNCATE TABLE imdb_title_akas_staging;
            TRUNCATE TABLE imdb_title_episode_staging;
            TRUNCATE TABLE imdb_title_ratings_staging;
            DELETE FROM data_source_import_runs;
        ");
    }

    private static ImdbStagingRepository CreateRepository(AppDbContext context)
    {
        return new ImdbStagingRepository(context, NullLogger<ImdbStagingRepository>.Instance);
    }

    // ---- BulkInsertBasicsAsync ----

    [Fact]
    public async Task BulkInsertBasicsAsync_InsertsRecords()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        var entities = new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "movie", PrimaryTitle = "Movie A", OriginalTitle = "Movie A", IsAdult = false, StartYear = 2020, EndYear = null, RuntimeMinutes = 120, Genres = "Drama" },
            new() { Tconst = "tt0000002", TitleType = "tvSeries", PrimaryTitle = "Series B", OriginalTitle = null, IsAdult = false, StartYear = 2021, EndYear = 2023, RuntimeMinutes = null, Genres = "Comedy,Action" },
        };

        await repo.BulkInsertBasicsAsync(entities, CancellationToken.None);

        var count = await context.ImdbTitleBasicsStaging.CountAsync();
        count.Should().Be(2);

        var first = await context.ImdbTitleBasicsStaging.FirstAsync(x => x.Tconst == "tt0000001");
        first.TitleType.Should().Be("movie");
        first.PrimaryTitle.Should().Be("Movie A");
        first.StartYear.Should().Be(2020);
        first.RuntimeMinutes.Should().Be(120);
        first.Genres.Should().Be("Drama");

        var second = await context.ImdbTitleBasicsStaging.FirstAsync(x => x.Tconst == "tt0000002");
        second.OriginalTitle.Should().BeNull();
        second.EndYear.Should().Be(2023);
        second.RuntimeMinutes.Should().BeNull();
    }

    [Fact]
    public async Task BulkInsertBasicsAsync_EmptyList_NoError()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>(), CancellationToken.None);

        var count = await context.ImdbTitleBasicsStaging.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsertBasicsAsync_MultipleBatches_Accumulates()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "movie", PrimaryTitle = "A", OriginalTitle = "A", IsAdult = false }
        }, CancellationToken.None);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000002", TitleType = "movie", PrimaryTitle = "B", OriginalTitle = "B", IsAdult = false }
        }, CancellationToken.None);

        (await context.ImdbTitleBasicsStaging.CountAsync()).Should().Be(2);
    }

    // ---- BulkInsertAkasAsync ----

    [Fact]
    public async Task BulkInsertAkasAsync_InsertsRecords()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        var entities = new List<ImdbTitleAkasStaging>
        {
            new() { Tconst = "tt0000001", Ordering = 1, Title = "Title A", Region = "US", Language = "en", Types = "imdbDisplay", Attributes = null, IsOriginalTitle = true },
            new() { Tconst = "tt0000001", Ordering = 2, Title = "Title B", Region = "CZ", Language = "cs", Types = null, Attributes = null, IsOriginalTitle = false },
        };

        await repo.BulkInsertAkasAsync(entities, CancellationToken.None);

        var count = await context.ImdbTitleAkasStaging.CountAsync();
        count.Should().Be(2);

        var first = await context.ImdbTitleAkasStaging.FirstAsync(x => x.Ordering == 1);
        first.Title.Should().Be("Title A");
        first.Region.Should().Be("US");
        first.IsOriginalTitle.Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsertAkasAsync_EmptyList_NoError()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertAkasAsync(new List<ImdbTitleAkasStaging>(), CancellationToken.None);

        (await context.ImdbTitleAkasStaging.CountAsync()).Should().Be(0);
    }

    // ---- BulkInsertEpisodeAsync ----

    [Fact]
    public async Task BulkInsertEpisodeAsync_InsertsRecords()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        var entities = new List<ImdbTitleEpisodeStaging>
        {
            new() { Tconst = "tt0000010", ParentTconst = "tt0000001", SeasonNumber = 1, EpisodeNumber = 1 },
            new() { Tconst = "tt0000011", ParentTconst = "tt0000001", SeasonNumber = 1, EpisodeNumber = 2 },
        };

        await repo.BulkInsertEpisodeAsync(entities, CancellationToken.None);

        var count = await context.ImdbTitleEpisodeStaging.CountAsync();
        count.Should().Be(2);

        var first = await context.ImdbTitleEpisodeStaging.FirstAsync(x => x.Tconst == "tt0000010");
        first.ParentTconst.Should().Be("tt0000001");
        first.SeasonNumber.Should().Be(1);
        first.EpisodeNumber.Should().Be(1);
    }

    [Fact]
    public async Task BulkInsertEpisodeAsync_EmptyList_NoError()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertEpisodeAsync(new List<ImdbTitleEpisodeStaging>(), CancellationToken.None);

        (await context.ImdbTitleEpisodeStaging.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BulkInsertEpisodeAsync_NullableFields()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        var entities = new List<ImdbTitleEpisodeStaging>
        {
            new() { Tconst = "tt0000010", ParentTconst = "tt0000001", SeasonNumber = null, EpisodeNumber = null },
        };

        await repo.BulkInsertEpisodeAsync(entities, CancellationToken.None);

        var row = await context.ImdbTitleEpisodeStaging.FirstAsync(x => x.Tconst == "tt0000010");
        row.SeasonNumber.Should().BeNull();
        row.EpisodeNumber.Should().BeNull();
    }

    // ---- BulkInsertRatingsAsync ----

    [Fact]
    public async Task BulkInsertRatingsAsync_InsertsRecords()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        var entities = new List<ImdbTitleRatingsStaging>
        {
            new() { Tconst = "tt0000001", AverageRating = 7.5m, NumVotes = 1000 },
            new() { Tconst = "tt0000002", AverageRating = 8.2m, NumVotes = 5000 },
        };

        await repo.BulkInsertRatingsAsync(entities, CancellationToken.None);

        var count = await context.ImdbTitleRatingsStaging.CountAsync();
        count.Should().Be(2);

        var first = await context.ImdbTitleRatingsStaging.FirstAsync(x => x.Tconst == "tt0000001");
        first.AverageRating.Should().Be(7.5m);
        first.NumVotes.Should().Be(1000);
    }

    [Fact]
    public async Task BulkInsertRatingsAsync_EmptyList_NoError()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertRatingsAsync(new List<ImdbTitleRatingsStaging>(), CancellationToken.None);

        (await context.ImdbTitleRatingsStaging.CountAsync()).Should().Be(0);
    }

    // ---- UpsertToLiveTablesAsync ----

    [Fact]
    public async Task UpsertToLiveTablesAsync_InsertsMovieWithRatings()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "movie", PrimaryTitle = "Test Movie", OriginalTitle = "Test Movie", IsAdult = false, StartYear = 2024, Genres = "Drama" }
        }, CancellationToken.None);

        await repo.BulkInsertRatingsAsync(new List<ImdbTitleRatingsStaging>
        {
            new() { Tconst = "tt0000001", AverageRating = 8.0m, NumVotes = 2000 }
        }, CancellationToken.None);

        await repo.UpsertToLiveTablesAsync(CancellationToken.None);

        // Verify media_titles
        var title = await context.MediaTitles.FirstOrDefaultAsync(t => t.CanonicalTitle == "Test Movie");
        title.Should().NotBeNull();
        title!.Type.Should().Be(MediaType.Movie);
        title.Year.Should().Be(2024);

        // Verify imdb_title_details
        var details = await context.ImdbTitleDetails.FirstOrDefaultAsync(d => d.Tconst == "tt0000001");
        details.Should().NotBeNull();
        details!.GenreString.Should().Be("Drama");

        // Verify media_ratings
        var rating = await context.MediaRatings.FirstOrDefaultAsync(r => r.MediaId == title.MediaId);
        rating.Should().NotBeNull();
        rating!.Rating.Should().Be(8.0m);
        rating.VoteCount.Should().Be(2000);
    }

    [Fact]
    public async Task UpsertToLiveTablesAsync_InsertsTvSeriesWithAliases()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "tvSeries", PrimaryTitle = "Test Series", OriginalTitle = "Test Series", IsAdult = false, StartYear = 2022, Genres = "Comedy" }
        }, CancellationToken.None);

        await repo.BulkInsertAkasAsync(new List<ImdbTitleAkasStaging>
        {
            new() { Tconst = "tt0000001", Ordering = 1, Title = "Test Série", Region = "CZ", Language = "cs", IsOriginalTitle = false }
        }, CancellationToken.None);

        await repo.UpsertToLiveTablesAsync(CancellationToken.None);

        var series = await context.MediaTitles.FirstOrDefaultAsync(t => t.CanonicalTitle == "Test Series");
        series.Should().NotBeNull();
        series!.Type.Should().Be(MediaType.Series);

        var aliases = await context.MediaTitleAliases.Where(a => a.MediaId == series.MediaId).ToListAsync();
        aliases.Should().ContainSingle();
        aliases[0].AliasTitle.Should().Be("Test Série");
        aliases[0].Region.Should().Be("CZ");
    }

    [Fact]
    public async Task UpsertToLiveTablesAsync_FiltersNonMatchingTitleTypes()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        // tvEpisode should be filtered out by the WHERE clause
        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000099", TitleType = "tvEpisode", PrimaryTitle = "Some Episode", OriginalTitle = "Some Episode", IsAdult = false, StartYear = 2024, Genres = "Drama" }
        }, CancellationToken.None);

        await repo.UpsertToLiveTablesAsync(CancellationToken.None);

        var count = await context.MediaTitles.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task UpsertToLiveTablesAsync_UpdatesExistingOnReimport()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        // First import
        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "movie", PrimaryTitle = "Movie X", OriginalTitle = "Movie X", IsAdult = false, StartYear = 2024, Genres = "Drama" }
        }, CancellationToken.None);
        await repo.BulkInsertRatingsAsync(new List<ImdbTitleRatingsStaging>
        {
            new() { Tconst = "tt0000001", AverageRating = 6.0m, NumVotes = 100 }
        }, CancellationToken.None);
        await repo.UpsertToLiveTablesAsync(CancellationToken.None);

        var title = await context.MediaTitles.FirstAsync(t => t.CanonicalTitle == "Movie X");
        var firstUpdatedAt = title.UpdatedAt;
        var rating = await context.MediaRatings.FirstAsync(r => r.MediaId == title.MediaId);
        rating.Rating.Should().Be(6.0m);

        // Cleanup staging and re-import with updated data
        await repo.CleanupStagingTablesAsync(CancellationToken.None);
        await Task.Delay(100);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "movie", PrimaryTitle = "Movie X", OriginalTitle = "Movie X", IsAdult = false, StartYear = 2024, Genres = "Thriller" }
        }, CancellationToken.None);
        await repo.BulkInsertRatingsAsync(new List<ImdbTitleRatingsStaging>
        {
            new() { Tconst = "tt0000001", AverageRating = 7.5m, NumVotes = 500 }
        }, CancellationToken.None);
        await repo.UpsertToLiveTablesAsync(CancellationToken.None);

        // Verify updated_at changed
        await context.Entry(title).ReloadAsync();
        title.UpdatedAt.Should().BeOnOrAfter(firstUpdatedAt);

        // Verify rating updated
        await context.Entry(rating).ReloadAsync();
        rating.Rating.Should().Be(7.5m);
        rating.VoteCount.Should().Be(500);

        // Verify genre_string updated
        var details = await context.ImdbTitleDetails.FirstAsync(d => d.MediaId == title.MediaId);
        details.GenreString.Should().Be("Thriller");
    }

    [Fact]
    public async Task UpsertToLiveTablesAsync_HandlesTvMiniSeriesAndTvMovie()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "tvMiniSeries", PrimaryTitle = "Mini Series", OriginalTitle = "Mini Series", IsAdult = false, StartYear = 2024, Genres = "Drama" },
            new() { Tconst = "tt0000002", TitleType = "tvMovie", PrimaryTitle = "TV Movie", OriginalTitle = "TV Movie", IsAdult = false, StartYear = 2024, Genres = "Comedy" },
        }, CancellationToken.None);

        await repo.UpsertToLiveTablesAsync(CancellationToken.None);

        var miniSeries = await context.MediaTitles.FirstOrDefaultAsync(t => t.CanonicalTitle == "Mini Series");
        miniSeries.Should().NotBeNull();
        miniSeries!.Type.Should().Be(MediaType.Series);

        var tvMovie = await context.MediaTitles.FirstOrDefaultAsync(t => t.CanonicalTitle == "TV Movie");
        tvMovie.Should().NotBeNull();
        tvMovie!.Type.Should().Be(MediaType.Movie);
    }

    // ---- CleanupStagingTablesAsync ----

    [Fact]
    public async Task CleanupStagingTablesAsync_TruncatesAllStagingTables()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.BulkInsertBasicsAsync(new List<ImdbTitleBasicsStaging>
        {
            new() { Tconst = "tt0000001", TitleType = "movie", PrimaryTitle = "A", OriginalTitle = "A", IsAdult = false }
        }, CancellationToken.None);
        await repo.BulkInsertAkasAsync(new List<ImdbTitleAkasStaging>
        {
            new() { Tconst = "tt0000001", Ordering = 1, Title = "B", IsOriginalTitle = false }
        }, CancellationToken.None);
        await repo.BulkInsertEpisodeAsync(new List<ImdbTitleEpisodeStaging>
        {
            new() { Tconst = "tt0000010", ParentTconst = "tt0000001" }
        }, CancellationToken.None);
        await repo.BulkInsertRatingsAsync(new List<ImdbTitleRatingsStaging>
        {
            new() { Tconst = "tt0000001", AverageRating = 5.0m, NumVotes = 100 }
        }, CancellationToken.None);

        // Verify data exists
        (await context.ImdbTitleBasicsStaging.CountAsync()).Should().BeGreaterThan(0);

        await repo.CleanupStagingTablesAsync(CancellationToken.None);

        (await context.ImdbTitleBasicsStaging.CountAsync()).Should().Be(0);
        (await context.ImdbTitleAkasStaging.CountAsync()).Should().Be(0);
        (await context.ImdbTitleEpisodeStaging.CountAsync()).Should().Be(0);
        (await context.ImdbTitleRatingsStaging.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CleanupStagingTablesAsync_EmptyTables_NoError()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        await repo.CleanupStagingTablesAsync(CancellationToken.None);

        (await context.ImdbTitleBasicsStaging.CountAsync()).Should().Be(0);
    }

    // ---- GetConnectionString null path ----

    [Fact]
    public async Task BulkInsertBasicsAsync_NullConnectionString_ThrowsInvalidOperation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var repo = CreateRepository(context);

        await repo.Invoking(r => r.BulkInsertBasicsAsync(
                new List<ImdbTitleBasicsStaging>(), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task BulkInsertAkasAsync_NullConnectionString_ThrowsInvalidOperation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var repo = CreateRepository(context);

        await repo.Invoking(r => r.BulkInsertAkasAsync(
                new List<ImdbTitleAkasStaging>(), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task BulkInsertEpisodeAsync_NullConnectionString_ThrowsInvalidOperation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var repo = CreateRepository(context);

        await repo.Invoking(r => r.BulkInsertEpisodeAsync(
                new List<ImdbTitleEpisodeStaging>(), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task BulkInsertRatingsAsync_NullConnectionString_ThrowsInvalidOperation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var repo = CreateRepository(context);

        await repo.Invoking(r => r.BulkInsertRatingsAsync(
                new List<ImdbTitleRatingsStaging>(), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
