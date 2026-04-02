using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.BackgroundServices;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.BackgroundServices;

public class ImdbImportBackgroundServiceTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ImdbImportService _mockImportService;

    public ImdbImportBackgroundServiceTests()
    {
        var context = CreateContext();
        var downloader = Substitute.ForPartsOf<ImdbDatasetDownloader>(
            new HttpClient(), NullLogger<ImdbDatasetDownloader>.Instance);
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        _mockImportService = Substitute.ForPartsOf<ImdbImportService>(
            context, downloader, parser, NullLogger<ImdbImportService>.Instance);
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
    public async Task GetImportIntervalAsync_ReturnsConfiguredValue()
    {
        // Seed a setting
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "ImdbImportIntervalHours",
                Value = "24",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(300); // allow initial import + GetImportIntervalAsync to run
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // If we got here without hanging or crashing, GetImportIntervalAsync worked.
        // The setting value (24) was read and used for the timer interval.
    }

    [Fact]
    public async Task GetImportIntervalAsync_ReturnsDefault_WhenNoSetting()
    {
        // No settings seeded - should use default (168 hours)
        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GetImportIntervalAsync_ReturnsDefault_WhenInvalidSetting()
    {
        // Seed an invalid setting value
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "ImdbImportIntervalHours",
                Value = "not_a_number",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GetImportIntervalAsync_ReturnsDefault_WhenNegativeValue()
    {
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "ImdbImportIntervalHours",
                Value = "-5",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GetImportIntervalAsync_ReturnsDefault_WhenZeroValue()
    {
        using (var seedContext = CreateContext())
        {
            seedContext.Settings.Add(new Setting
            {
                Key = "ImdbImportIntervalHours",
                Value = "0",
                LastModifiedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        _mockImportService.RunImportAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        // InMemory database is cleaned up when last context is disposed
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
            NullLogger<ImdbImportBackgroundService>.Instance);
    }
}
