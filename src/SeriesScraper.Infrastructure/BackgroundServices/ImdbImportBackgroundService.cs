using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that runs IMDB dataset imports on a configurable schedule.
/// Uses PeriodicTimer per research issue #7.
/// AC#10, AC#11 from issue #22. Issue #101: auto-import + configurable refresh.
/// </summary>
public class ImdbImportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IImdbImportTrigger _importTrigger;
    private readonly ILogger<ImdbImportBackgroundService> _logger;
    internal const string RefreshIntervalSettingKey = "imdb.refresh_interval";
    internal const string DefaultRefreshInterval = "weekly";
    internal const int ImdbSourceId = 1;
    
    public ImdbImportBackgroundService(
        IServiceProvider serviceProvider,
        IImdbImportTrigger importTrigger,
        ILogger<ImdbImportBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _importTrigger = importTrigger;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IMDB Import Background Service starting");
        
        // Check if initial import is needed (no completed import exists)
        if (await IsInitialImportNeededAsync(stoppingToken))
        {
            _logger.LogInformation("No previous IMDB import found — running initial import");
            await RunImportAsync(stoppingToken);
        }
        else
        {
            _logger.LogInformation("IMDB data already imported — skipping initial import");
        }
        
        // Then run on schedule (or wait for manual trigger)
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = await GetRefreshIntervalAsync(stoppingToken);
            
            if (interval is null)
            {
                // "manual" mode — only respond to explicit triggers
                _logger.LogInformation("IMDB refresh set to manual — waiting for trigger");
                try
                {
                    if (await _importTrigger.WaitForTriggerAsync(stoppingToken))
                    {
                        await RunImportAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            else
            {
                _logger.LogInformation("Next IMDB import scheduled in {Interval}", interval.Value);
                
                using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                using var timer = new PeriodicTimer(interval.Value);
                
                try
                {
                    // Race: scheduled tick vs manual trigger
                    var timerTask = timer.WaitForNextTickAsync(timerCts.Token).AsTask();
                    var triggerTask = _importTrigger.WaitForTriggerAsync(timerCts.Token);
                    
                    await Task.WhenAny(timerTask, triggerTask);
                    timerCts.Cancel(); // Cancel the other waiter
                    
                    if (stoppingToken.IsCancellationRequested)
                        break;
                    
                    await RunImportAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        
        _logger.LogInformation("IMDB Import Background Service stopping");
    }
    
    internal async Task<bool> IsInitialImportNeededAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            
            return !await context.DataSourceImportRuns
                .AnyAsync(r => r.SourceId == ImdbSourceId && r.Status == "Complete", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check IMDB import status, assuming import needed");
            return true;
        }
    }
    
    private async Task RunImportAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting IMDB import");
            
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImdbImportService>();
            
            var importRunId = await importService.RunImportAsync(cancellationToken);
            
            _logger.LogInformation("IMDB import completed successfully (run ID: {ImportRunId})", importRunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IMDB import failed");
            // Don't throw - continue running for next scheduled import
        }
    }
    
    internal async Task<TimeSpan?> GetRefreshIntervalAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            
            var setting = await context.Settings
                .FirstOrDefaultAsync(s => s.Key == RefreshIntervalSettingKey, cancellationToken);
            
            var intervalValue = setting?.Value ?? DefaultRefreshInterval;
            return ConvertIntervalToTimeSpan(intervalValue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read refresh interval from settings, using default (weekly)");
            return TimeSpan.FromHours(168);
        }
    }
    
    internal static TimeSpan? ConvertIntervalToTimeSpan(string interval)
    {
        return interval?.ToLowerInvariant() switch
        {
            "daily" => TimeSpan.FromHours(24),
            "weekly" => TimeSpan.FromHours(168),
            "monthly" => TimeSpan.FromHours(720),
            "manual" => null,
            _ => TimeSpan.FromHours(168) // Default to weekly for unknown values
        };
    }
}
