using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class ScrapeRunItemRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public ScrapeRunItemRepositoryTests(PostgresqlFixture fixture)
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
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_run_items");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_runs");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forum_sections");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forums");
    }

    private static ScrapeRunItemRepository CreateRepository(AppDbContext context)
        => new(context);

    private async Task<Forum> SeedForumAsync()
    {
        await using var context = _fixture.CreateContext();
        var forum = new Forum
        {
            Name = "TestForum",
            BaseUrl = "https://testforum.example.com",
            Username = "testuser",
            CredentialKey = "FORUM_TEST_KEY",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Set<Forum>().Add(forum);
        await context.SaveChangesAsync();
        return forum;
    }

    private async Task<ScrapeRun> SeedScrapeRunAsync(int forumId, ScrapeRunStatus status = ScrapeRunStatus.Running)
    {
        await using var context = _fixture.CreateContext();
        var run = new ScrapeRun
        {
            ForumId = forumId,
            Status = status,
            StartedAt = DateTime.UtcNow,
            TotalItems = 0,
            ProcessedItems = 0
        };
        context.Set<ScrapeRun>().Add(run);
        await context.SaveChangesAsync();
        return run;
    }

    private async Task<ScrapeRunItem> SeedRunItemAsync(int runId, string postUrl, ScrapeRunItemStatus status = ScrapeRunItemStatus.Pending)
    {
        await using var context = _fixture.CreateContext();
        var item = new ScrapeRunItem
        {
            RunId = runId,
            PostUrl = postUrl,
            Status = status,
            ProcessedAt = status != ScrapeRunItemStatus.Pending ? DateTime.UtcNow : null
        };
        context.Set<ScrapeRunItem>().Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    // ── GetByRunIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByRunIdAsync_ReturnsItemsForRun()
    {
        var forum = await SeedForumAsync();
        var run = await SeedScrapeRunAsync(forum.ForumId);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/1", ScrapeRunItemStatus.Pending);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/2", ScrapeRunItemStatus.Done);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByRunIdAsync(run.RunId);

        result.Should().HaveCount(2);
        result.Select(i => i.PostUrl).Should().Contain("https://forum.example.com/post/1");
        result.Select(i => i.PostUrl).Should().Contain("https://forum.example.com/post/2");
    }

    [Fact]
    public async Task GetByRunIdAsync_ReturnsEmpty_WhenNoItems()
    {
        var forum = await SeedForumAsync();
        var run = await SeedScrapeRunAsync(forum.ForumId);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByRunIdAsync(run.RunId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRunIdAsync_DoesNotReturnItemsFromOtherRuns()
    {
        var forum = await SeedForumAsync();
        var run1 = await SeedScrapeRunAsync(forum.ForumId);
        var run2 = await SeedScrapeRunAsync(forum.ForumId);
        await SeedRunItemAsync(run1.RunId, "https://forum.example.com/post/1");
        await SeedRunItemAsync(run2.RunId, "https://forum.example.com/post/2");

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByRunIdAsync(run1.RunId);

        result.Should().HaveCount(1);
        result[0].PostUrl.Should().Be("https://forum.example.com/post/1");
    }

    [Fact]
    public async Task GetByRunIdAsync_ReturnsOrderedByRunItemId()
    {
        var forum = await SeedForumAsync();
        var run = await SeedScrapeRunAsync(forum.ForumId);
        var item1 = await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/a");
        var item2 = await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/b");
        var item3 = await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/c");

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByRunIdAsync(run.RunId);

        result.Should().HaveCount(3);
        result[0].RunItemId.Should().Be(item1.RunItemId);
        result[1].RunItemId.Should().Be(item2.RunItemId);
        result[2].RunItemId.Should().Be(item3.RunItemId);
    }

    // ── CountByRunIdAndStatusAsync ───────────────────────────────────────

    [Fact]
    public async Task CountByRunIdAndStatusAsync_ReturnsCorrectCount()
    {
        var forum = await SeedForumAsync();
        var run = await SeedScrapeRunAsync(forum.ForumId);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/1", ScrapeRunItemStatus.Done);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/2", ScrapeRunItemStatus.Done);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/3", ScrapeRunItemStatus.Failed);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var doneCount = await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Done);
        var failedCount = await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Failed);

        doneCount.Should().Be(2);
        failedCount.Should().Be(1);
    }

    [Fact]
    public async Task CountByRunIdAndStatusAsync_ReturnsZero_WhenNoMatch()
    {
        var forum = await SeedForumAsync();
        var run = await SeedScrapeRunAsync(forum.ForumId);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/1", ScrapeRunItemStatus.Done);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var count = await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Pending);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountByRunIdAndStatusAsync_DoesNotCountOtherRuns()
    {
        var forum = await SeedForumAsync();
        var run1 = await SeedScrapeRunAsync(forum.ForumId);
        var run2 = await SeedScrapeRunAsync(forum.ForumId);
        await SeedRunItemAsync(run1.RunId, "https://forum.example.com/post/1", ScrapeRunItemStatus.Done);
        await SeedRunItemAsync(run2.RunId, "https://forum.example.com/post/2", ScrapeRunItemStatus.Done);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var count = await repo.CountByRunIdAndStatusAsync(run1.RunId, ScrapeRunItemStatus.Done);

        count.Should().Be(1);
    }

    [Fact]
    public async Task CountByRunIdAndStatusAsync_AllStatusValues()
    {
        var forum = await SeedForumAsync();
        var run = await SeedScrapeRunAsync(forum.ForumId);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/1", ScrapeRunItemStatus.Pending);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/2", ScrapeRunItemStatus.Processing);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/3", ScrapeRunItemStatus.Done);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/4", ScrapeRunItemStatus.Failed);
        await SeedRunItemAsync(run.RunId, "https://forum.example.com/post/5", ScrapeRunItemStatus.Skipped);

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        (await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Pending)).Should().Be(1);
        (await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Processing)).Should().Be(1);
        (await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Done)).Should().Be(1);
        (await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Failed)).Should().Be(1);
        (await repo.CountByRunIdAndStatusAsync(run.RunId, ScrapeRunItemStatus.Skipped)).Should().Be(1);
    }
}
