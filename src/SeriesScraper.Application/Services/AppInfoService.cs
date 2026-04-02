using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class AppInfoService : IAppInfoService
{
    private readonly IDatabaseStatsProvider _statsProvider;
    private readonly ILogger<AppInfoService> _logger;
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public AppInfoService(
        IDatabaseStatsProvider statsProvider,
        ILogger<AppInfoService> logger)
    {
        _statsProvider = statsProvider;
        _logger = logger;
    }

    public async Task<DatabaseStatsDto> GetDatabaseStatsAsync(CancellationToken ct = default)
    {
        var counts = await _statsProvider.GetTableRowCountsAsync(ct);
        return new DatabaseStatsDto { TableCounts = counts };
    }

    public async Task<AppInfoDto> GetAppInfoAsync(CancellationToken ct = default)
    {
        var version = typeof(AppInfoService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var uptime = DateTime.UtcNow - StartTime;
        var dbConnected = await _statsProvider.CheckConnectionAsync(ct);
        var dbStats = await GetDatabaseStatsAsync(ct);

        return new AppInfoDto
        {
            Version = version,
            Uptime = uptime,
            DatabaseStats = dbStats,
            DatabaseConnected = dbConnected
        };
    }
}
