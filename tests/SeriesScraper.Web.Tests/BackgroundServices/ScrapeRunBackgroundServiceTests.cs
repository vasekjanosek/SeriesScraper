using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;
using SeriesScraper.Infrastructure.Services;
using SeriesScraper.Web.BackgroundServices;

namespace SeriesScraper.Web.Tests.BackgroundServices;

public class ScrapeRunBackgroundServiceTests
{
    private readonly IScrapingJobQueue _jobQueue;
    private readonly IScrapeRunService _scrapeRunService;
    private readonly ILogger<ScrapeRunBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ScrapeRunBackgroundServiceTests()
    {
        _jobQueue = new ScrapingJobQueue();
        _scrapeRunService = Substitute.For<IScrapeRunService>();
        _logger = Substitute.For<ILogger<ScrapeRunBackgroundService>>();
        _scopeFactory = CreateScopeFactory();
    }

    private IServiceScopeFactory CreateScopeFactory()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IScrapeRunService)).Returns(_scrapeRunService);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        return factory;
    }

    [Fact]
    public async Task ExecuteAsync_MarksInterruptedRunsOnStartup()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new ScrapeRunBackgroundService(_jobQueue, _scopeFactory, _logger);

        // Start the service, let it run briefly then cancel
        var task = sut.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();

        try { await task; } catch (OperationCanceledException) { }

        await _scrapeRunService.Received(1).MarkInterruptedRunsAsPartialAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesEnqueuedJob()
    {
        var job = new ScrapeJob { RunId = 1, ForumId = 1 };
        await _jobQueue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sut = new ScrapeRunBackgroundService(_jobQueue, _scopeFactory, _logger);

        var serviceTask = sut.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try { await serviceTask; } catch (OperationCanceledException) { }

        await _scrapeRunService.Received(1).ProcessJobAsync(
            Arg.Is<ScrapeJob>(j => j.RunId == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MarksAsPartialOnUserCancellation()
    {
        // Set up the service to throw OperationCanceledException when processing
        using var jobCts = new CancellationTokenSource();
        var job = new ScrapeJob { RunId = 5, ForumId = 1, CancellationTokenSource = jobCts };

        _scrapeRunService.ProcessJobAsync(Arg.Any<ScrapeJob>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                jobCts.Cancel();
                ci.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        await _jobQueue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sut = new ScrapeRunBackgroundService(_jobQueue, _scopeFactory, _logger);

        var serviceTask = sut.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try { await serviceTask; } catch (OperationCanceledException) { }

        await _scrapeRunService.Received(1).MarkRunAsPartialAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MarksAsFailedOnException()
    {
        var job = new ScrapeJob { RunId = 3, ForumId = 1 };

        _scrapeRunService.ProcessJobAsync(Arg.Any<ScrapeJob>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _jobQueue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sut = new ScrapeRunBackgroundService(_jobQueue, _scopeFactory, _logger);

        var serviceTask = sut.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try { await serviceTask; } catch (OperationCanceledException) { }

        await _scrapeRunService.Received(1).FailRunAsync(3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesMultipleJobsSequentially()
    {
        var job1 = new ScrapeJob { RunId = 1, ForumId = 1 };
        var job2 = new ScrapeJob { RunId = 2, ForumId = 2 };

        await _jobQueue.EnqueueAsync(job1);
        await _jobQueue.EnqueueAsync(job2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sut = new ScrapeRunBackgroundService(_jobQueue, _scopeFactory, _logger);

        var serviceTask = sut.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try { await serviceTask; } catch (OperationCanceledException) { }

        await _scrapeRunService.Received(1).ProcessJobAsync(
            Arg.Is<ScrapeJob>(j => j.RunId == 1), Arg.Any<CancellationToken>());
        await _scrapeRunService.Received(1).ProcessJobAsync(
            Arg.Is<ScrapeJob>(j => j.RunId == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterJobFailure()
    {
        var job1 = new ScrapeJob { RunId = 1, ForumId = 1 };
        var job2 = new ScrapeJob { RunId = 2, ForumId = 2 };

        // First job fails, second should still be processed
        _scrapeRunService.ProcessJobAsync(Arg.Is<ScrapeJob>(j => j.RunId == 1), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("fail"));
        _scrapeRunService.ProcessJobAsync(Arg.Is<ScrapeJob>(j => j.RunId == 2), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _jobQueue.EnqueueAsync(job1);
        await _jobQueue.EnqueueAsync(job2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sut = new ScrapeRunBackgroundService(_jobQueue, _scopeFactory, _logger);

        var serviceTask = sut.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try { await serviceTask; } catch (OperationCanceledException) { }

        await _scrapeRunService.Received(1).FailRunAsync(1, Arg.Any<CancellationToken>());
        await _scrapeRunService.Received(1).ProcessJobAsync(
            Arg.Is<ScrapeJob>(j => j.RunId == 2), Arg.Any<CancellationToken>());
    }
}
