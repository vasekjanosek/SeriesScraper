using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class ScrapeRunHistoryRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public ScrapeRunHistoryRepositoryTests(PostgresqlFixture fixture)
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
        await context.Database.ExecuteSqlRawAsync("DELETE FROM links");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_run_items");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_runs");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forum_sections");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forums");
    }

    private static ScrapeRunRepository CreateRepository(AppDbContext context)
        => new(context);

    private async Task<Forum> SeedForumAsync(string name = "TestForum")
    {
        await using var context = _fixture.CreateContext();
        var forum = new Forum
        {
            Name = name,
            BaseUrl = $"https://{name.ToLowerInvariant()}.example.com",
            Username = "testuser",
            CredentialKey = "FORUM_TEST_KEY",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Forums.Add(forum);
        await context.SaveChangesAsync();
        return forum;
    }

    private async Task<ScrapeRun> SeedRunAsync(
        int forumId,
        ScrapeRunStatus status = ScrapeRunStatus.Complete,
        int totalItems = 10,
        int processedItems = 10,
        DateTime? startedAt = null,
        DateTime? completedAt = null)
    {
        await using var context = _fixture.CreateContext();
        var now = DateTime.UtcNow;
        var run = new ScrapeRun
        {
            ForumId = forumId,
            Status = status,
            TotalItems = totalItems,
            ProcessedItems = processedItems,
            StartedAt = startedAt ?? now.AddMinutes(-5),
            CompletedAt = completedAt ?? (status == ScrapeRunStatus.Complete || status == ScrapeRunStatus.Failed || status == ScrapeRunStatus.Partial ? now : null)
        };
        context.ScrapeRuns.Add(run);
        await context.SaveChangesAsync();
        return run;
    }

    private async Task SeedLinkAsync(int runId)
    {
        await using var context = _fixture.CreateContext();
        var link = new Link
        {
            Url = $"https://download.example.com/{Guid.NewGuid()}",
            LinkTypeId = 1, // seeded by migration
            PostUrl = $"https://forum.example.com/post/{Guid.NewGuid()}",
            RunId = runId,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Links.Add(link);
        await context.SaveChangesAsync();
    }

    private async Task SeedRunItemAsync(int runId, int? itemId = null)
    {
        await using var context = _fixture.CreateContext();
        var item = new ScrapeRunItem
        {
            RunId = runId,
            PostUrl = $"https://forum.example.com/post/{Guid.NewGuid()}",
            Status = ScrapeRunItemStatus.Done,
            ItemId = itemId,
            ProcessedAt = DateTime.UtcNow
        };
        context.Set<ScrapeRunItem>().Add(item);
        await context.SaveChangesAsync();
    }

    // ── GetRunHistoryPagedAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetRunHistoryPagedAsync_ReturnsCompletedRuns()
    {
        var forum = await SeedForumAsync();
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete);
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Failed);
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Pending); // should be excluded

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto(), page: 1, pageSize: 50, sortBy: null, sortDescending: true);

        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.Status == "Complete" || i.Status == "Failed");
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_ExcludesRunningAndPending()
    {
        var forum = await SeedForumAsync();
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Running);
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Pending);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto(), page: 1, pageSize: 50, sortBy: null, sortDescending: true);

        totalCount.Should().Be(0);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_StatusFilter()
    {
        var forum = await SeedForumAsync();
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete);
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Failed);
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Partial);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto { StatusFilter = "Failed" }, page: 1, pageSize: 50, sortBy: null, sortDescending: true);

        totalCount.Should().Be(1);
        items.Should().ContainSingle().Which.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_Pagination_Page1()
    {
        var forum = await SeedForumAsync();
        for (int i = 0; i < 5; i++)
            await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete,
                startedAt: DateTime.UtcNow.AddMinutes(-50 + i * 10),
                completedAt: DateTime.UtcNow.AddMinutes(-45 + i * 10));

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto(), page: 1, pageSize: 2, sortBy: null, sortDescending: false);

        totalCount.Should().Be(5);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_Pagination_Page3()
    {
        var forum = await SeedForumAsync();
        for (int i = 0; i < 5; i++)
            await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete,
                startedAt: DateTime.UtcNow.AddMinutes(-50 + i * 10),
                completedAt: DateTime.UtcNow.AddMinutes(-45 + i * 10));

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto(), page: 3, pageSize: 2, sortBy: null, sortDescending: false);

        items.Should().HaveCount(1); // 5 items, page 3 of 2 = 1 remaining
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_SortByDuration()
    {
        var forum = await SeedForumAsync();
        var now = DateTime.UtcNow;
        // Short run: 1 minute
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete,
            startedAt: now.AddMinutes(-1), completedAt: now);
        // Long run: 10 minutes
        await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete,
            startedAt: now.AddMinutes(-10), completedAt: now);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto(), page: 1, pageSize: 50, sortBy: "duration", sortDescending: true);

        items.Should().HaveCount(2);
        // Descending: longest first
        items[0].Duration.Should().BeGreaterThan(items[1].Duration!.Value);
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_SortByForumName()
    {
        var forumA = await SeedForumAsync("AlphaForum");
        var forumZ = await SeedForumAsync("ZetaForum");
        await SeedRunAsync(forumZ.ForumId, ScrapeRunStatus.Complete);
        await SeedRunAsync(forumA.ForumId, ScrapeRunStatus.Complete);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto(), page: 1, pageSize: 50, sortBy: "forum", sortDescending: false);

        items.Should().HaveCount(2);
        items[0].ForumName.Should().Be("AlphaForum");
        items[1].ForumName.Should().Be("ZetaForum");
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_LinkCountAndMatchCount()
    {
        var forum = await SeedForumAsync();
        var run = await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete);
        await SeedLinkAsync(run.RunId);
        await SeedLinkAsync(run.RunId);
        await SeedRunItemAsync(run.RunId, itemId: 42); // has ItemId → counts as match
        await SeedRunItemAsync(run.RunId, itemId: null); // no ItemId → not a match

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, _) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto(), page: 1, pageSize: 50, sortBy: null, sortDescending: true);

        items.Should().ContainSingle();
        items[0].LinkCount.Should().Be(2);
        items[0].MatchCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRunHistoryPagedAsync_ForumIdFilter()
    {
        var forum1 = await SeedForumAsync("Forum1");
        var forum2 = await SeedForumAsync("Forum2");
        await SeedRunAsync(forum1.ForumId, ScrapeRunStatus.Complete);
        await SeedRunAsync(forum2.ForumId, ScrapeRunStatus.Complete);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var (items, totalCount) = await repo.GetRunHistoryPagedAsync(
            new RunHistoryFilterDto { ForumId = forum1.ForumId }, page: 1, pageSize: 50, sortBy: null, sortDescending: true);

        totalCount.Should().Be(1);
        items.Should().ContainSingle().Which.ForumName.Should().Be("Forum1");
    }

    // ── GetRunSummaryByIdAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetRunSummaryByIdAsync_ReturnsSummary()
    {
        var forum = await SeedForumAsync();
        var run = await SeedRunAsync(forum.ForumId, ScrapeRunStatus.Complete, totalItems: 20, processedItems: 18);
        await SeedLinkAsync(run.RunId);
        await SeedRunItemAsync(run.RunId, itemId: 1);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var summary = await repo.GetRunSummaryByIdAsync(run.RunId);

        summary.Should().NotBeNull();
        summary!.RunId.Should().Be(run.RunId);
        summary.ForumName.Should().Be("TestForum");
        summary.Status.Should().Be("Complete");
        summary.TotalItems.Should().Be(20);
        summary.ProcessedItems.Should().Be(18);
        summary.LinkCount.Should().Be(1);
        summary.MatchCount.Should().Be(1);
        summary.Duration.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRunSummaryByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var summary = await repo.GetRunSummaryByIdAsync(999999);

        summary.Should().BeNull();
    }
}
