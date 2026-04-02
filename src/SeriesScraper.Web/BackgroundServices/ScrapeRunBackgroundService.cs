using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Web.BackgroundServices;

/// <summary>
/// Thin-shell BackgroundService per ADR-003.
/// Contains ONLY lifecycle wiring — all business logic is in IScrapeRunService.
/// </summary>
public class ScrapeRunBackgroundService : BackgroundService
{
    private readonly IScrapingJobQueue _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScrapeRunBackgroundService> _logger;

    public ScrapeRunBackgroundService(
        IScrapingJobQueue jobQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<ScrapeRunBackgroundService> logger)
    {
        _jobQueue = jobQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScrapeRunBackgroundService starting");

        // AC#6: On app restart, any ScrapeRun with status=Running is set to Partial
        await MarkInterruptedRunsAsync(stoppingToken);

        await foreach (var job in _jobQueue.DequeueAllAsync(stoppingToken))
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken, job.CancellationTokenSource.Token);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IScrapeRunService>();
                await service.ProcessJobAsync(job, linkedCts.Token);
            }
            catch (OperationCanceledException) when (job.CancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("Scrape run {RunId} was cancelled by user", job.RunId);
                await SetRunPartialAsync(job.RunId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Host shutting down, scrape run {RunId} interrupted", job.RunId);
                await SetRunPartialAsync(job.RunId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scrape run {RunId} failed with unhandled error", job.RunId);
                await SetRunFailedAsync(job.RunId);
            }
            finally
            {
                job.CancellationTokenSource.Dispose();
            }
        }

        _logger.LogInformation("ScrapeRunBackgroundService stopped");
    }

    private async Task MarkInterruptedRunsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScrapeRunService>();
            await service.MarkInterruptedRunsAsPartialAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark interrupted runs as Partial on startup");
        }
    }

    private async Task SetRunPartialAsync(int runId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScrapeRunService>();
            await service.MarkRunAsPartialAsync(runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark run {RunId} as Partial", runId);
        }
    }

    private async Task SetRunFailedAsync(int runId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScrapeRunService>();
            await service.FailRunAsync(runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark run {RunId} as Failed", runId);
        }
    }
}
