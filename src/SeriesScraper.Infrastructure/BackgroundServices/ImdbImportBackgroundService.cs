using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that runs IMDB dataset imports on a configurable schedule.
/// Uses PeriodicTimer per research issue #7.
/// AC#10, AC#11 from issue #22.
/// </summary>
public class ImdbImportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImdbImportBackgroundService> _logger;
    private const string SettingKey = "imdb.refresh_interval";
    private const int DefaultIntervalHours = 168; // 7 days
    
    public ImdbImportBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ImdbImportBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IMDB Import Background Service starting");
        
        // Run initial import on startup
        await RunImportAsync(stoppingToken);
        
        // Then run on schedule
        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalHours = await GetImportIntervalAsync(stoppingToken);
            var interval = TimeSpan.FromHours(intervalHours);
            
            _logger.LogInformation("Next IMDB import scheduled in {Interval}", interval);
            
            using var timer = new PeriodicTimer(interval);
            
            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunImportAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
        }
        
        _logger.LogInformation("IMDB Import Background Service stopping");
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
    
    private async Task<int> GetImportIntervalAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            
            var setting = await context.Settings
                .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);
            
            if (setting != null && int.TryParse(setting.Value, out var hours) && hours > 0)
            {
                return hours;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read import interval from settings, using default");
        }
        
        return DefaultIntervalHours;
    }
}
