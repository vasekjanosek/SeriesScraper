using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Repository for querying ScrapeRunItems by run ID and status.
/// </summary>
public interface IScrapeRunItemRepository
{
    Task<IReadOnlyList<ScrapeRunItem>> GetByRunIdAsync(int runId, CancellationToken ct = default);
    Task<int> CountByRunIdAndStatusAsync(int runId, ScrapeRunItemStatus status, CancellationToken ct = default);
}
