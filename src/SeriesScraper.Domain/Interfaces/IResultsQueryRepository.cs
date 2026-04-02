using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Interfaces;

public interface IResultsQueryRepository
{
    Task<(IReadOnlyList<ResultSummaryDto> Items, int TotalCount)> GetPagedResultsAsync(
        ResultFilterDto filter,
        int page,
        int pageSize,
        string? sortBy,
        bool sortDescending,
        CancellationToken ct = default);

    Task<ScrapeRunItem?> GetRunItemByIdAsync(int runItemId, CancellationToken ct = default);

    Task<IReadOnlyList<Link>> GetLinksForRunItemAsync(int runId, string postUrl, CancellationToken ct = default);
}
