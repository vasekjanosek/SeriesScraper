using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class ResultsQueryRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public ResultsQueryRepositoryTests(PostgresqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await EnsureSchemaExtensionsAsync();
        await CleanupAsync();
        await SeedBaseDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// The Link entity defines PostUrl but the migration on this branch hasn't been updated.
    /// Add the column if missing so the repository queries work against real PostgreSQL.
    /// </summary>
    private async Task EnsureSchemaExtensionsAsync()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(@"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'links' AND column_name = 'post_url'
                ) THEN
                    ALTER TABLE links ADD COLUMN post_url character varying(2000) NOT NULL DEFAULT '';
                    CREATE INDEX IF NOT EXISTS ix_links_run_id_post_url_is_current
                        ON links (run_id, post_url, is_current);
                END IF;
            END $$;
        ");
    }

    private async Task CleanupAsync()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM links");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_run_items");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_runs");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forum_sections");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forums");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_title_aliases");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_episodes");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_ratings");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM imdb_title_details");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_titles");
    }

    private async Task SeedBaseDataAsync()
    {
        await using var context = _fixture.CreateContext();

        var forum = new Forum
        {
            ForumId = 1,
            Name = "Test Forum",
            BaseUrl = "https://forum.example.com",
            Username = "testuser",
            CredentialKey = "TEST_CREDENTIAL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Forums.Add(forum);

        var run = new ScrapeRun
        {
            RunId = 1,
            ForumId = 1,
            Status = ScrapeRunStatus.Complete,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            TotalItems = 5,
            ProcessedItems = 5
        };
        context.ScrapeRuns.Add(run);

        await context.SaveChangesAsync();

        // DataSource SourceId=1 (IMDB) and LinkTypes 1-4 are seeded by migrations

        // Seed MediaTitles
        var movie = new MediaTitle
        {
            CanonicalTitle = "The Matrix",
            Year = 1999,
            Type = MediaType.Movie,
            SourceId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var series = new MediaTitle
        {
            CanonicalTitle = "Breaking Bad",
            Year = 2008,
            Type = MediaType.Series,
            SourceId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.MediaTitles.AddRange(movie, series);
        await context.SaveChangesAsync();

        // Seed ScrapeRunItems with different statuses
        var items = new[]
        {
            new ScrapeRunItem { RunId = 1, PostUrl = "https://forum.example.com/post/1", ItemId = movie.MediaId, Status = ScrapeRunItemStatus.Done, ProcessedAt = DateTime.UtcNow.AddMinutes(-50) },
            new ScrapeRunItem { RunId = 1, PostUrl = "https://forum.example.com/post/2", ItemId = series.MediaId, Status = ScrapeRunItemStatus.Done, ProcessedAt = DateTime.UtcNow.AddMinutes(-40) },
            new ScrapeRunItem { RunId = 1, PostUrl = "https://forum.example.com/post/3", ItemId = null, Status = ScrapeRunItemStatus.Failed, ProcessedAt = DateTime.UtcNow.AddMinutes(-30) },
            new ScrapeRunItem { RunId = 1, PostUrl = "https://forum.example.com/post/4", ItemId = null, Status = ScrapeRunItemStatus.Pending },
            new ScrapeRunItem { RunId = 1, PostUrl = "https://forum.example.com/post/5", ItemId = movie.MediaId, Status = ScrapeRunItemStatus.Done, ProcessedAt = DateTime.UtcNow.AddMinutes(-20) },
        };
        context.ScrapeRunItems.AddRange(items);
        await context.SaveChangesAsync();

        // Use migration-seeded LinkType (ID=1 "Direct HTTP")
        // Seed Links for first item
        context.Links.AddRange(
            new Link { Url = "https://dl.example.com/1a", LinkTypeId = 1, PostUrl = items[0].PostUrl, RunId = 1, IsCurrent = true, CreatedAt = DateTime.UtcNow },
            new Link { Url = "https://dl.example.com/1b", LinkTypeId = 1, PostUrl = items[0].PostUrl, RunId = 1, IsCurrent = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new Link { Url = "https://dl.example.com/1old", LinkTypeId = 1, PostUrl = items[0].PostUrl, RunId = 1, IsCurrent = false, CreatedAt = DateTime.UtcNow.AddHours(-1) }
        );
        // One link for second item
        context.Links.Add(
            new Link { Url = "https://dl.example.com/2a", LinkTypeId = 1, PostUrl = items[1].PostUrl, RunId = 1, IsCurrent = true, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();
    }

    private static ResultsQueryRepository CreateRepository(AppDbContext context)
        => new(context);

    // ── GetPagedResultsAsync — Pagination ─────────────────────────────────

    [Fact]
    public async Task GetPagedResultsAsync_ReturnsCorrectPageSize()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetPagedResultsAsync(
            new ResultFilterDto(), page: 1, pageSize: 2, sortBy: null, sortDescending: false);

        items.Should().HaveCount(2);
        totalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedResultsAsync_SecondPage_ReturnsRemainingItems()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetPagedResultsAsync(
            new ResultFilterDto(), page: 2, pageSize: 3, sortBy: null, sortDescending: false);

        items.Should().HaveCount(2); // 5 total, page 2 of size 3 = 2 remaining
        totalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedResultsAsync_PageBeyondData_ReturnsEmpty()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto(), page: 10, pageSize: 5, sortBy: null, sortDescending: false);

        items.Should().BeEmpty();
    }

    // ── GetPagedResultsAsync — Filters ────────────────────────────────────

    [Fact]
    public async Task GetPagedResultsAsync_FilterByRunId_ReturnsOnlyThatRun()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { RunId = 1 }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        totalCount.Should().Be(5);
        items.Should().OnlyContain(i => i.RunId == 1);
    }

    [Fact]
    public async Task GetPagedResultsAsync_FilterByRunId_NonExistent_ReturnsEmpty()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { RunId = 999 }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        totalCount.Should().Be(0);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedResultsAsync_FilterByContentType_Movie_ReturnsMoviesOnly()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { ContentType = "Movie" }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        items.Should().OnlyContain(i => i.MediaType == "Movie");
    }

    [Fact]
    public async Task GetPagedResultsAsync_FilterByContentType_Series_ReturnsSeriesOnly()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { ContentType = "Series" }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        items.Should().OnlyContain(i => i.MediaType == "Series");
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedResultsAsync_FilterByStatus_DoneOnly()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { StatusFilter = "Done" }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        totalCount.Should().Be(3);
        items.Should().OnlyContain(i => i.Status == "Done");
    }

    [Fact]
    public async Task GetPagedResultsAsync_FilterByTitleSearch_MatchesTitle()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { TitleSearch = "matrix" }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        items.Should().OnlyContain(i => i.MatchedTitle != null && i.MatchedTitle.Contains("Matrix"));
    }

    [Fact]
    public async Task GetPagedResultsAsync_FilterByTitleSearch_MatchesPostUrl()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { TitleSearch = "post/3" }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        items.Should().HaveCount(1);
        items[0].PostUrl.Should().Contain("post/3");
    }

    // ── GetPagedResultsAsync — Sorting ────────────────────────────────────

    [Fact]
    public async Task GetPagedResultsAsync_SortByTitle_Ascending()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { StatusFilter = "Done" }, page: 1, pageSize: 10, sortBy: "title", sortDescending: false);

        var titles = items.Select(i => i.MatchedTitle).ToList();
        titles.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetPagedResultsAsync_SortByTitle_Descending()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { StatusFilter = "Done" }, page: 1, pageSize: 10, sortBy: "title", sortDescending: true);

        var titles = items.Select(i => i.MatchedTitle).ToList();
        titles.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetPagedResultsAsync_SortByStatus_ThrowsDueToEnumToStringTranslation()
    {
        // Known issue: Status enum projected via .ToString() can't be translated
        // server-side by EF Core when combined with OrderBy.
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var act = () => repo.GetPagedResultsAsync(
            new ResultFilterDto(), page: 1, pageSize: 10, sortBy: "status", sortDescending: false);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be translated*");
    }

    [Fact]
    public async Task GetPagedResultsAsync_SortByLinks()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto(), page: 1, pageSize: 10, sortBy: "links", sortDescending: true);

        var linkCounts = items.Select(i => i.LinkCount).ToList();
        linkCounts.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetPagedResultsAsync_SortByMediaType_ThrowsDueToEnumToStringTranslation()
    {
        // Known issue: MediaType enum projected via .ToString() can't be translated
        // server-side by EF Core when combined with OrderBy.
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var act = () => repo.GetPagedResultsAsync(
            new ResultFilterDto(), page: 1, pageSize: 10, sortBy: "mediatype", sortDescending: false);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be translated*");
    }

    [Fact]
    public async Task GetPagedResultsAsync_SortByProcessedAt()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { StatusFilter = "Done" }, page: 1, pageSize: 10, sortBy: "processedat", sortDescending: false);

        var dates = items.Select(i => i.ProcessedAt).ToList();
        dates.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetPagedResultsAsync_DefaultSort_NewestFirst()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto(), page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        var ids = items.Select(i => i.RunItemId).ToList();
        ids.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetPagedResultsAsync_LinkCount_CountsOnlyCurrentLinks()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetPagedResultsAsync(
            new ResultFilterDto { TitleSearch = "post/1" }, page: 1, pageSize: 10, sortBy: null, sortDescending: false);

        // post/1 has 2 current + 1 historical — only current should be counted
        items.Should().HaveCount(1);
        items[0].LinkCount.Should().Be(2);
    }

    // ── GetRunItemByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetRunItemByIdAsync_ExistingItem_ReturnsItem()
    {
        await using var seedCtx = _fixture.CreateContext();
        var runItemId = await seedCtx.ScrapeRunItems
            .Where(ri => ri.PostUrl.Contains("post/1"))
            .Select(ri => ri.RunItemId)
            .FirstAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetRunItemByIdAsync(runItemId);

        result.Should().NotBeNull();
        result!.PostUrl.Should().Contain("post/1");
    }

    [Fact]
    public async Task GetRunItemByIdAsync_NonExistent_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetRunItemByIdAsync(999999);

        result.Should().BeNull();
    }

    // ── GetLinksForRunItemAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetLinksForRunItemAsync_ReturnsLinksWithLinkType()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLinksForRunItemAsync(1, "https://forum.example.com/post/1");

        result.Should().HaveCount(3); // 2 current + 1 historical
        result.Should().OnlyContain(l => l.LinkType != null);
        result.First().LinkType.Name.Should().Be("Direct HTTP");
    }

    [Fact]
    public async Task GetLinksForRunItemAsync_OrdersByCurrentThenRecentFirst()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLinksForRunItemAsync(1, "https://forum.example.com/post/1");

        // Current links first, then non-current
        var currentLinks = result.TakeWhile(l => l.IsCurrent).ToList();
        var historicalLinks = result.SkipWhile(l => l.IsCurrent).ToList();
        currentLinks.Should().HaveCount(2);
        historicalLinks.Should().HaveCount(1);
        historicalLinks[0].IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task GetLinksForRunItemAsync_NoLinks_ReturnsEmpty()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetLinksForRunItemAsync(1, "https://forum.example.com/post/4");

        result.Should().BeEmpty();
    }
}
