using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Provides real-time progress information for scrape runs.
/// </summary>
public class RunProgressService : IRunProgressService
{
    private readonly IScrapeRunRepository _runRepository;
    private readonly IScrapeRunItemRepository _itemRepository;
    private readonly ILogger<RunProgressService> _logger;

    public RunProgressService(
        IScrapeRunRepository runRepository,
        IScrapeRunItemRepository itemRepository,
        ILogger<RunProgressService> logger)
    {
        _runRepository = runRepository;
        _itemRepository = itemRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RunProgressDto>> GetActiveRunsAsync(CancellationToken ct = default)
    {
        var activeRuns = await _runRepository.GetActiveRunsAsync(ct);
        var results = new List<RunProgressDto>(activeRuns.Count);

        foreach (var run in activeRuns)
        {
            results.Add(MapToDto(run));
        }

        _logger.LogDebug("Retrieved {Count} active runs", results.Count);
        return results;
    }

    public async Task<RunProgressDto?> GetRunProgressAsync(int scrapeRunId, CancellationToken ct = default)
    {
        var run = await _runRepository.GetByIdAsync(scrapeRunId, ct);
        if (run is null)
        {
            _logger.LogWarning("Scrape run {RunId} not found", scrapeRunId);
            return null;
        }

        // GetByIdAsync includes Items, but load them via item repository for consistency
        var items = await _itemRepository.GetByRunIdAsync(scrapeRunId, ct);

        var dto = MapToDto(run, items);
        _logger.LogDebug("Retrieved progress for run {RunId}: {Processed}/{Total}", scrapeRunId, dto.ProcessedItems, dto.TotalItems);
        return dto;
    }

    private static RunProgressDto MapToDto(Domain.Entities.ScrapeRun run, IReadOnlyList<Domain.Entities.ScrapeRunItem>? items = null)
    {
        var runItems = items ?? (IReadOnlyList<Domain.Entities.ScrapeRunItem>)run.Items.ToList();

        var completedItems = runItems.Count(i => i.Status == ScrapeRunItemStatus.Done);
        var failedItems = runItems.Count(i => i.Status == ScrapeRunItemStatus.Failed);
        var pendingItems = runItems.Count(i => i.Status == ScrapeRunItemStatus.Pending);
        var currentItem = runItems
            .FirstOrDefault(i => i.Status == ScrapeRunItemStatus.Processing)
            ?.PostUrl;

        return new RunProgressDto
        {
            RunId = run.RunId,
            ForumId = run.ForumId,
            ForumName = run.Forum?.Name ?? $"Forum #{run.ForumId}",
            Status = run.Status,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            TotalItems = run.TotalItems,
            ProcessedItems = run.ProcessedItems,
            CompletedItems = completedItems,
            FailedItems = failedItems,
            PendingItems = pendingItems,
            CurrentItem = currentItem,
            Items = runItems.Select(i => new RunItemProgressDto
            {
                RunItemId = i.RunItemId,
                PostUrl = i.PostUrl,
                Status = i.Status,
                ProcessedAt = i.ProcessedAt
            }).ToList()
        };
    }
}
