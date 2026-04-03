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
}
