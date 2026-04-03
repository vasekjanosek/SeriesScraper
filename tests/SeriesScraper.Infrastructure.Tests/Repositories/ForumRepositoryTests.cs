using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
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
}
