using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class HistoryService : IHistoryService
{
    private readonly IScrapeRunRepository _scrapeRunRepository;
    private readonly ILogger<HistoryService> _logger;

    public HistoryService(
        IScrapeRunRepository scrapeRunRepository,
        ILogger<HistoryService> logger)
    {
        _scrapeRunRepository = scrapeRunRepository;
        _logger = logger;
    }

    public async Task<PagedResult<RunHistorySummaryDto>> GetRunHistoryAsync(
        RunHistoryFilterDto filter,
        int page,
        int pageSize,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _scrapeRunRepository.GetRunHistoryPagedAsync(
            filter, page, pageSize, sortBy, sortDescending, ct);

        return new PagedResult<RunHistorySummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<RunHistorySummaryDto?> GetRunSummaryAsync(
        int scrapeRunId,
        CancellationToken ct = default)
    {
        return await _scrapeRunRepository.GetRunSummaryByIdAsync(scrapeRunId, ct);
    }
}
