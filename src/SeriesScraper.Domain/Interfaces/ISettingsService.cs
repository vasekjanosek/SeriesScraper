using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface ISettingsService
{
    Task<IReadOnlyList<Setting>> GetAllSettingsAsync(CancellationToken ct = default);
    Task UpdateSettingAsync(string key, string value, CancellationToken ct = default);
    Task<ImdbImportStatusDto> GetImdbImportStatusAsync(CancellationToken ct = default);
}

public record ImdbImportStatusDto
{
    public DateTime? LastImportDate { get; init; }
    public long RowsImported { get; init; }
    public string? Status { get; init; }
    public DateTime? NextScheduledRun { get; init; }
}
