using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository contract for application settings access.
/// </summary>
public interface ISettingRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(string key, string value, CancellationToken ct = default);
}
