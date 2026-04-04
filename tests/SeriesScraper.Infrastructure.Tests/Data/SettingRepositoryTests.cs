using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class SettingRepositoryTests
{
    private static (AppDbContext context, SettingRepository repo) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        var repo = new SettingRepository(context);
        return (context, repo);
    }

    [Fact]
    public void Constructor_NullContext_ThrowsArgumentNullException()
    {
        var act = () => new SettingRepository(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetValueAsync_ExistingKey_ReturnsValue()
    {
        var (context, repo) = CreateSut();
        using (context)
        {
            var result = await repo.GetValueAsync("QualityPruningThreshold");

            result.Should().NotBeNull();
            result.Should().Be("5");
        }
    }

    [Fact]
    public async Task GetValueAsync_NonExistingKey_ReturnsNull()
    {
        var (context, repo) = CreateSut();
        using (context)
        {
            var result = await repo.GetValueAsync("NonExistentKey");

            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetValueAsync_KnownSeedSettings_AllExist()
    {
        var (context, repo) = CreateSut();
        using (context)
        {
            var imdb = await repo.GetValueAsync("imdb.refresh_interval");
            imdb.Should().NotBeNull();

            var maxThreads = await repo.GetValueAsync("MaxConcurrentScrapeThreads");
            maxThreads.Should().NotBeNull();
        }
    }

    // ─── Zero-config defaults (#106) ──────────────────────────────

    [Fact]
    public async Task GetValueAsync_ZeroConfigDefaults_AllNewKeysSeeded()
    {
        var (context, repo) = CreateSut();
        using (context)
        {
            (await repo.GetValueAsync("scrape.request_delay")).Should().Be("2000");
            (await repo.GetValueAsync("results.page_size")).Should().Be("25");
            (await repo.GetValueAsync("forum.default_encoding")).Should().Be("windows-1250");
            (await repo.GetValueAsync("language.filter")).Should().Be("all");
        }
    }

    [Fact]
    public async Task GetValueAsync_ForumRefreshIntervalHours_IsSeeded()
    {
        var (context, repo) = CreateSut();
        using (context)
        {
            var value = await repo.GetValueAsync("ForumRefreshIntervalHours");
            value.Should().Be("24");
        }
    }

    [Fact]
    public async Task GetAllAsync_ContainsAllExpectedSettings()
    {
        var (context, repo) = CreateSut();
        using (context)
        {
            var all = await repo.GetAllAsync();

            var expectedKeys = new[]
            {
                "ImdbRefreshIntervalHours",
                "ForumStructureRefreshIntervalHours",
                "MaxConcurrentScrapeThreads",
                "QualityPruningThreshold",
                "ResultRetentionDays",
                "HttpRetryCount",
                "HttpRetryBackoffMultiplier",
                "HttpCircuitBreakerThreshold",
                "HttpTimeoutSeconds",
                "BulkImportMemoryCeilingMB",
                "ForumRefreshIntervalHours",
                "scrape.request_delay",
                "results.page_size",
                "forum.default_encoding",
                "language.filter"
            };

            all.Select(s => s.Key).Should().Contain(expectedKeys);
        }
    }
}
