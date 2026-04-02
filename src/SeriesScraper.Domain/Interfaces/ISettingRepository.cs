namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository contract for application settings access.
/// </summary>
public interface ISettingRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);
}
