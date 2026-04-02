using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Service for querying real-time progress of scrape runs.
/// </summary>
public interface IRunProgressService
{
    Task<IReadOnlyList<RunProgressDto>> GetActiveRunsAsync(CancellationToken ct = default);
    Task<RunProgressDto?> GetRunProgressAsync(int scrapeRunId, CancellationToken ct = default);
}
