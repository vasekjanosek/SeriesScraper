using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class ForumRepositoryTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public ForumRepositoryTests(PostgresqlFixture fixture)
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
        await context.Database.ExecuteSqlRawAsync("DELETE FROM scrape_runs");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forum_sections");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM forums");
    }

    private static ForumRepository CreateRepository(AppDbContext context)
        => new(context);

    private static Forum CreateForum(int id, string name, bool isActive = true) => new()
    {
        ForumId = id,
        Name = name,
        BaseUrl = $"https://{name.ToLowerInvariant().Replace(" ", "")}.example.com",
        Username = "testuser",
        CredentialKey = "FORUM_TEST_CREDENTIAL",
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ── GetByIdAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsForum()
    {
        await using var seedContext = _fixture.CreateContext();
        var forum = CreateForum(100, "Test Forum");
        seedContext.Forums.Add(forum);
        await seedContext.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByIdAsync(100);

        result.Should().NotBeNull();
        result!.ForumId.Should().Be(100);
        result.Name.Should().Be("Test Forum");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ── GetActiveAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveForums()
    {
        await using var seedContext = _fixture.CreateContext();
        seedContext.Forums.AddRange(
            CreateForum(200, "Active One", isActive: true),
            CreateForum(201, "Inactive One", isActive: false),
            CreateForum(202, "Active Two", isActive: true)
        );
        await seedContext.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetActiveAsync();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(f => f.IsActive);
        result.Select(f => f.Name).Should().Contain("Active One").And.Contain("Active Two");
    }

    [Fact]
    public async Task GetActiveAsync_NoActiveForums_ReturnsEmpty()
    {
        await using var seedContext = _fixture.CreateContext();
        seedContext.Forums.Add(CreateForum(300, "Inactive Only", isActive: false));
        await seedContext.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetActiveAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_NoForumsAtAll_ReturnsEmpty()
    {
        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetActiveAsync();

        result.Should().BeEmpty();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllForums_OrderedByName()
    {
        await using var seedContext = _fixture.CreateContext();
        seedContext.Forums.AddRange(
            CreateForum(400, "Zulu Forum"),
            CreateForum(401, "Alpha Forum"),
            CreateForum(402, "Mike Forum", isActive: false)
        );
        await seedContext.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(3);
        result.Select(f => f.Name).Should().ContainInOrder("Alpha Forum", "Mike Forum", "Zulu Forum");
    }

    // ── AddAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidForum_PersistsToDatabase()
    {
        var forum = CreateForum(500, "New Forum");

        await using var addContext = _fixture.CreateContext();
        var repo = CreateRepository(addContext);
        await repo.AddAsync(forum);

        await using var verifyContext = _fixture.CreateContext();
        var persisted = await verifyContext.Forums.FirstOrDefaultAsync(f => f.ForumId == 500);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("New Forum");
        persisted.BaseUrl.Should().Be("https://newforum.example.com");
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingForum_ModifiesRecord()
    {
        await using var seedContext = _fixture.CreateContext();
        seedContext.Forums.Add(CreateForum(600, "Original Name"));
        await seedContext.SaveChangesAsync();

        await using var updateContext = _fixture.CreateContext();
        var existing = await updateContext.Forums.FirstAsync(f => f.ForumId == 600);
        existing.Name = "Updated Name";
        var repo = CreateRepository(updateContext);
        await repo.UpdateAsync(existing);

        await using var verifyContext = _fixture.CreateContext();
        var updated = await verifyContext.Forums.FirstOrDefaultAsync(f => f.ForumId == 600);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingForum_RemovesFromDatabase()
    {
        await using var seedContext = _fixture.CreateContext();
        seedContext.Forums.Add(CreateForum(700, "To Delete"));
        await seedContext.SaveChangesAsync();

        await using var deleteContext = _fixture.CreateContext();
        var existing = await deleteContext.Forums.FirstAsync(f => f.ForumId == 700);
        var repo = CreateRepository(deleteContext);
        await repo.DeleteAsync(existing);

        await using var verifyContext = _fixture.CreateContext();
        var deleted = await verifyContext.Forums.FirstOrDefaultAsync(f => f.ForumId == 700);

        deleted.Should().BeNull();
    }

    // ── DenormalizeForumNameOnRunsAsync ────────────────────────────────────

    [Fact]
    public async Task DenormalizeForumNameOnRunsAsync_UpdatesRunsWithForumId()
    {
        await using var seedContext = _fixture.CreateContext();
        var forum = CreateForum(800, "Old Forum Name");
        seedContext.Forums.Add(forum);
        await seedContext.SaveChangesAsync();

        seedContext.ScrapeRuns.AddRange(
            new ScrapeRun { ForumId = 800, ForumName = "Old Forum Name", Status = ScrapeRunStatus.Pending, StartedAt = DateTime.UtcNow },
            new ScrapeRun { ForumId = 800, ForumName = "Old Forum Name", Status = ScrapeRunStatus.Pending, StartedAt = DateTime.UtcNow }
        );
        await seedContext.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        await repo.DenormalizeForumNameOnRunsAsync(800, "New Forum Name");

        await using var verifyContext = _fixture.CreateContext();
        var runs = await verifyContext.ScrapeRuns.Where(r => r.ForumId == 800).ToListAsync();

        runs.Should().HaveCount(2);
        runs.Should().OnlyContain(r => r.ForumName == "New Forum Name");
    }

    [Fact]
    public async Task DenormalizeForumNameOnRunsAsync_OnlyAffectsSpecifiedForumRuns()
    {
        await using var seedContext = _fixture.CreateContext();
        seedContext.Forums.AddRange(
            CreateForum(900, "Forum A"),
            CreateForum(901, "Forum B")
        );
        await seedContext.SaveChangesAsync();

        seedContext.ScrapeRuns.AddRange(
            new ScrapeRun { ForumId = 900, ForumName = "Forum A", Status = ScrapeRunStatus.Pending, StartedAt = DateTime.UtcNow },
            new ScrapeRun { ForumId = 901, ForumName = "Forum B", Status = ScrapeRunStatus.Pending, StartedAt = DateTime.UtcNow }
        );
        await seedContext.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repo = CreateRepository(context);
        await repo.DenormalizeForumNameOnRunsAsync(900, "Forum A Renamed");

        await using var verifyContext = _fixture.CreateContext();
        var forumARuns = await verifyContext.ScrapeRuns.Where(r => r.ForumId == 900).ToListAsync();
        var forumBRuns = await verifyContext.ScrapeRuns.Where(r => r.ForumId == 901).ToListAsync();

        forumARuns.Should().OnlyContain(r => r.ForumName == "Forum A Renamed");
        forumBRuns.Should().OnlyContain(r => r.ForumName == "Forum B");
    }
}
