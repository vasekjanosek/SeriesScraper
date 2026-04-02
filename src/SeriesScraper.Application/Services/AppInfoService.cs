using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class AppInfoService : IAppInfoService
{
    private readonly ISettingRepository _settingRepository;
    private readonly IDataSourceImportRunRepository _importRunRepository;
    private readonly ILogger<AppInfoService> _logger;
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public AppInfoService(
        ISettingRepository settingRepository,
        IDataSourceImportRunRepository importRunRepository,
        ILogger<AppInfoService> logger)
    {
        _settingRepository = settingRepository;
        _importRunRepository = importRunRepository;
        _logger = logger;
    }

    public async Task<DatabaseStatsDto> GetDatabaseStatsAsync(CancellationToken ct = default)
    {
        // Settings table count serves as a health check
        var settings = await _settingRepository.GetAllAsync(ct);

        var counts = new List<TableRowCount>
        {
            new() { TableName = "Settings", RowCount = settings.Count }
        };

        return new DatabaseStatsDto { TableCounts = counts };
    }

    public Task<AppInfoDto> GetAppInfoAsync(CancellationToken ct = default)
    {
        var version = typeof(AppInfoService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var uptime = DateTime.UtcNow - StartTime;

        var info = new AppInfoDto
        {
            Version = version,
            Uptime = uptime,
            DatabaseStats = new DatabaseStatsDto()
        };

        return Task.FromResult(info);
    }
}
