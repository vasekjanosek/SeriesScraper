using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;

namespace SeriesScraper.Infrastructure.Tests.Repositories;

[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class DatabaseStatsProviderTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public DatabaseStatsProviderTests(PostgresqlFixture fixture)
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
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_title_aliases");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_episodes");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_ratings");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM imdb_title_details");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM media_titles");
    }

    private static DatabaseStatsProvider CreateProvider(AppDbContext context)
        => new(context);

    // ── CheckConnectionAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CheckConnectionAsync_ReturnsTrue_WhenDatabaseIsUp()
    {
        await using var context = _fixture.CreateContext();
        var provider = CreateProvider(context);

        var result = await provider.CheckConnectionAsync();

        result.Should().BeTrue();
    }

    // ── GetTableRowCountsAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetTableRowCountsAsync_Returns6Tables()
    {
        await using var context = _fixture.CreateContext();
        var provider = CreateProvider(context);

        var counts = await provider.GetTableRowCountsAsync();

        counts.Should().HaveCount(6);
        counts.Select(c => c.TableName).Should().Contain(new[]
        {
            "Forums", "MediaTitles", "Links", "ScrapeRuns", "Settings", "LinkTypes"
        });
    }

    [Fact]
    public async Task GetTableRowCountsAsync_ReturnsZeroCounts_WhenTablesEmpty()
    {
        await using var context = _fixture.CreateContext();
        var provider = CreateProvider(context);

        var counts = await provider.GetTableRowCountsAsync();

        // Forums table cleaned up — should be zero
        counts.Single(c => c.TableName == "Forums").RowCount.Should().Be(0);
        counts.Single(c => c.TableName == "MediaTitles").RowCount.Should().Be(0);
        counts.Single(c => c.TableName == "Links").RowCount.Should().Be(0);
        counts.Single(c => c.TableName == "ScrapeRuns").RowCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTableRowCountsAsync_ReflectsInsertedRows()
    {
        // Seed a forum
        await using (var seedCtx = _fixture.CreateContext())
        {
            seedCtx.Forums.Add(new Forum
            {
                Name = "StatsTestForum",
                BaseUrl = "https://stats.example.com",
                Username = "user",
                CredentialKey = "KEY",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var provider = CreateProvider(context);

        var counts = await provider.GetTableRowCountsAsync();

        counts.Single(c => c.TableName == "Forums").RowCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTableRowCountsAsync_LinkTypesCount_IncludesSeedData()
    {
        // LinkTypes are seeded by migration — should be > 0
        await using var context = _fixture.CreateContext();
        var provider = CreateProvider(context);

        var counts = await provider.GetTableRowCountsAsync();

        counts.Single(c => c.TableName == "LinkTypes").RowCount.Should().BeGreaterThan(0);
    }
}
