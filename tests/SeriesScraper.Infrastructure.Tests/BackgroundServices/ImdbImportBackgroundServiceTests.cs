using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.BackgroundServices;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.BackgroundServices;

public class ImdbImportBackgroundServiceTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ImdbImportService _mockImportService;
    private readonly IImdbImportTrigger _mockTrigger;

    public ImdbImportBackgroundServiceTests()
    {
        var context = CreateContext();
        var downloader = Substitute.ForPartsOf<ImdbDatasetDownloader>(
            new HttpClient(), NullLogger<ImdbDatasetDownloader>.Instance);
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var stagingRepo = Substitute.For<IImdbStagingRepository>();

        _mockImportService = Substitute.ForPartsOf<ImdbImportService>(
            context, downloader, parser, stagingRepo, NullLogger<ImdbImportService>.Instance);
        
        _mockTrigger = Substitute.For<IImdbImportTrigger>();
        // By default, trigger never fires (blocks forever until cancelled)
        _mockTrigger.WaitForTriggerAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>()));
    }

    [Fact]
    public async Task ExecuteAsync_RunsInitialImportOnStart()
    {
        var importCalled = new TaskCompletionSource<bool>();
        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                importCalled.TrySetResult(true);
                return 1;
            });

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        var wasCalled = await importCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        wasCalled.Should().BeTrue();
        await _mockImportService.Received().RunImportAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefullyOnCancellation()
    {
        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(200); // allow initial import to complete
        cts.Cancel();

        // StopAsync should complete without throwing
        var act = async () => await service.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesOnImportFailure()
    {
        var importAttempted = new TaskCompletionSource<bool>();
        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns<int>(ci =>
            {
                importAttempted.TrySetResult(true);
                throw new InvalidOperationException("Download failed");
            });

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await importAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100); // give loop time to continue

        // Service should still be running (not crashed)
        cts.Cancel();
        var act = async () => await service.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsConfiguredValue()
    {
        // Seed a setting
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "imdb.refresh_interval",
                Value = "daily",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(24);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDefault_WhenNoSetting()
    {
        // No settings seeded - should use default (weekly = 168 hours)
        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(168);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDefault_WhenInvalidSetting()
    {
        // Seed an invalid setting value - should fall back to weekly default
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "imdb.refresh_interval",
                Value = "not_a_valid_interval",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(168);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsMonthlyTimeSpan_WhenMonthlySetting()
    {
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "imdb.refresh_interval",
                Value = "monthly",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(720);
    }

    public void Dispose()
    {
        // InMemory database is cleaned up when last context is disposed
    }

    // ─── ConvertIntervalToTimeSpan (#101) ─────────────────────────

    [Theory]
    [InlineData("daily", 24)]
    [InlineData("Daily", 24)]
    [InlineData("DAILY", 24)]
    [InlineData("weekly", 168)]
    [InlineData("Weekly", 168)]
    [InlineData("monthly", 720)]
    [InlineData("Monthly", 720)]
    [InlineData("unknown", 168)]
    public void ConvertIntervalToTimeSpan_ReturnsCorrectTimeSpan(string interval, int expectedHours)
    {
        var result = ImdbImportBackgroundService.ConvertIntervalToTimeSpan(interval);

        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(expectedHours);
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("Manual")]
    [InlineData("MANUAL")]
    public void ConvertIntervalToTimeSpan_ReturnsNull_ForManual(string interval)
    {
        var result = ImdbImportBackgroundService.ConvertIntervalToTimeSpan(interval);

        result.Should().BeNull();
    }

    [Fact]
    public void ConvertIntervalToTimeSpan_ReturnsWeeklyDefault_ForNullInput()
    {
        var result = ImdbImportBackgroundService.ConvertIntervalToTimeSpan(null!);

        // null falls through switch default → weekly
        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(168);
    }

    // ─── IsInitialImportNeededAsync (#101) ─────────────────────────

    [Fact]
    public async Task IsInitialImportNeeded_ReturnsTrue_WhenNoImportRuns()
    {
        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>()).Returns(1);
        var service = CreateService();

        var result = await service.IsInitialImportNeededAsync(CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsInitialImportNeeded_ReturnsFalse_WhenCompleteImportExists()
    {
        // Seed a completed import run
        using (var seedContext = CreateContext())
        {
            seedContext.DataSourceImportRuns.Add(new DataSourceImportRun
            {
                SourceId = 1,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                FinishedAt = DateTime.UtcNow,
                Status = "Complete",
                RowsImported = 5000
            });
            await seedContext.SaveChangesAsync();
        }

        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>()).Returns(1);
        var service = CreateService();

        var result = await service.IsInitialImportNeededAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsInitialImportNeeded_ReturnsTrue_WhenOnlyFailedImportsExist()
    {
        using (var seedContext = CreateContext())
        {
            seedContext.DataSourceImportRuns.Add(new DataSourceImportRun
            {
                SourceId = 1,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                FinishedAt = DateTime.UtcNow,
                Status = "Failed",
                RowsImported = 0,
                ErrorMessage = "Connection refused"
            });
            await seedContext.SaveChangesAsync();
        }

        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>()).Returns(1);
        var service = CreateService();

        var result = await service.IsInitialImportNeededAsync(CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SkipsInitialImport_WhenDataExists()
    {
        // Seed a completed import run
        using (var seedContext = CreateContext())
        {
            seedContext.DataSourceImportRuns.Add(new DataSourceImportRun
            {
                SourceId = 1,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                FinishedAt = DateTime.UtcNow,
                Status = "Complete",
                RowsImported = 5000
            });
            await seedContext.SaveChangesAsync();
        }

        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>()).Returns(1);
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Import should NOT have been called (data already exists)
        await _mockImportService.DidNotReceive().RunImportAsync(Arg.Any<CancellationToken>());
    }

    // ─── GetRefreshIntervalAsync (#101) ───────────────────────────

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsNull_WhenManual()
    {
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "imdb.refresh_interval",
                Value = "manual",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDailyTimeSpan()
    {
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "imdb.refresh_interval",
                Value = "daily",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(24);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsWeeklyDefault_WhenNoSetting()
    {
        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Value.TotalHours.Should().Be(168);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
        return new AppDbContext(options);
    }

    private ImdbImportBackgroundService CreateService()
    {
        var services = new ServiceCollection();

        // Register the mock import service so the BG service can resolve it
        services.AddSingleton(_mockImportService);

        // Register AppDbContext using the shared InMemory DB
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
        services.AddScoped(_ => new AppDbContext(dbOptions));

        var provider = services.BuildServiceProvider();
        return new ImdbImportBackgroundService(
            provider,
            _mockTrigger,
            NullLogger<ImdbImportBackgroundService>.Instance);
    }
}
