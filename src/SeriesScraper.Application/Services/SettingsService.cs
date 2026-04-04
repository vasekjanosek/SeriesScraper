using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly ISettingRepository _settingRepository;
    private readonly IDataSourceImportRunRepository _importRunRepository;
    private readonly ILogger<SettingsService> _logger;

    // IMDB source ID (seeded in migration)
    private const int ImdbSourceId = 1;

    public SettingsService(
        ISettingRepository settingRepository,
        IDataSourceImportRunRepository importRunRepository,
        ILogger<SettingsService> logger)
    {
        _settingRepository = settingRepository;
        _importRunRepository = importRunRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Setting>> GetAllSettingsAsync(CancellationToken ct = default)
    {
        return await _settingRepository.GetAllAsync(ct);
    }

    public async Task<string> GetSettingValueAsync(string key, string defaultValue, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be empty.", nameof(key));

        var value = await _settingRepository.GetValueAsync(key, ct);
        return value ?? defaultValue;
    }

    public async Task UpdateSettingAsync(string key, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be empty.", nameof(key));

        if (value is null)
            throw new ArgumentNullException(nameof(value));

        await _settingRepository.UpdateAsync(key, value, ct);
        _logger.LogInformation("Setting '{Key}' updated", key);
    }

    public async Task<ImdbImportStatusDto> GetImdbImportStatusAsync(CancellationToken ct = default)
    {
        var lastRun = await _importRunRepository.GetLastImportRunAsync(ImdbSourceId, ct);
        var refreshIntervalStr = await _settingRepository.GetValueAsync("imdb.refresh_interval", ct);

        DateTime? nextScheduled = null;
        if (lastRun?.FinishedAt is not null && int.TryParse(refreshIntervalStr, out var hours))
        {
            nextScheduled = lastRun.FinishedAt.Value.AddHours(hours);
        }

        return new ImdbImportStatusDto
        {
            LastImportDate = lastRun?.FinishedAt ?? lastRun?.StartedAt,
            RowsImported = lastRun?.RowsImported ?? 0,
            Status = lastRun?.Status,
            NextScheduledRun = nextScheduled
        };
    }
}
