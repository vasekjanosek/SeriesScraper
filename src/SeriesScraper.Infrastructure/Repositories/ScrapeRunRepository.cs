using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

public class ScrapeRunRepository : IScrapeRunRepository
{
    private readonly AppDbContext _context;

    public ScrapeRunRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ScrapeRun> CreateAsync(ScrapeRun run, CancellationToken ct = default)
    {
        _context.ScrapeRuns.Add(run);
        await _context.SaveChangesAsync(ct);
        return run;
    }

    public async Task<ScrapeRun?> GetByIdAsync(int runId, CancellationToken ct = default)
    {
        return await _context.ScrapeRuns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.RunId == runId, ct);
    }

    public async Task UpdateStatusAsync(int runId, ScrapeRunStatus status, DateTime? completedAt = null, CancellationToken ct = default)
    {
        var run = await _context.ScrapeRuns.FindAsync(new object[] { runId }, ct)
            ?? throw new InvalidOperationException($"ScrapeRun {runId} not found");

        run.Status = status;
        if (completedAt.HasValue)
            run.CompletedAt = completedAt.Value;

        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkRunningAsPartialAsync(CancellationToken ct = default)
    {
        var runningRuns = await _context.ScrapeRuns
            .Where(r => r.Status == ScrapeRunStatus.Running)
            .ToListAsync(ct);

        foreach (var run in runningRuns)
        {
            run.Status = ScrapeRunStatus.Partial;
            run.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetCompletedPostUrlsAsync(int runId, CancellationToken ct = default)
    {
        return await _context.Set<ScrapeRunItem>()
            .Where(i => i.RunId == runId && i.Status == ScrapeRunItemStatus.Done)
            .Select(i => i.PostUrl)
            .ToListAsync(ct);
    }

    public async Task IncrementProcessedItemsAsync(int runId, CancellationToken ct = default)
    {
        var run = await _context.ScrapeRuns.FindAsync(new object[] { runId }, ct)
            ?? throw new InvalidOperationException($"ScrapeRun {runId} not found");

        run.ProcessedItems++;
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddRunItemAsync(ScrapeRunItem item, CancellationToken ct = default)
    {
        _context.Set<ScrapeRunItem>().Add(item);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateRunItemStatusAsync(int runItemId, ScrapeRunItemStatus status, CancellationToken ct = default)
    {
        var item = await _context.Set<ScrapeRunItem>().FindAsync(new object[] { runItemId }, ct)
            ?? throw new InvalidOperationException($"ScrapeRunItem {runItemId} not found");

        item.Status = status;
        item.ProcessedAt = status is ScrapeRunItemStatus.Done or ScrapeRunItemStatus.Failed or ScrapeRunItemStatus.Skipped
            ? DateTime.UtcNow
            : null;

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScrapeRun>> GetActiveRunsAsync(CancellationToken ct = default)
    {
        return await _context.ScrapeRuns
            .Include(r => r.Forum)
            .Include(r => r.Items)
            .Where(r => r.Status == ScrapeRunStatus.Pending || r.Status == ScrapeRunStatus.Running)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<RunHistorySummaryDto> Items, int TotalCount)> GetRunHistoryPagedAsync(
        RunHistoryFilterDto filter, int page, int pageSize, string? sortBy, bool sortDescending, CancellationToken ct = default)
    {
        var query = _context.ScrapeRuns.AsNoTracking()
            .Include(r => r.Forum)
            .AsQueryable();

        // Filter: only completed runs (not Pending/Running)
        var historyStatuses = new[] { ScrapeRunStatus.Complete, ScrapeRunStatus.Failed, ScrapeRunStatus.Partial };
        query = query.Where(r => historyStatuses.Contains(r.Status));

        if (filter.ForumId.HasValue)
            query = query.Where(r => r.ForumId == filter.ForumId.Value);

        if (!string.IsNullOrEmpty(filter.StatusFilter) &&
            Enum.TryParse<ScrapeRunStatus>(filter.StatusFilter, true, out var statusEnum))
            query = query.Where(r => r.Status == statusEnum);

        if (filter.DateFrom.HasValue)
            query = query.Where(r => r.StartedAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(r => r.StartedAt <= filter.DateTo.Value);

        var totalCount = await query.CountAsync(ct);

        var projected = query.Select(r => new RunHistorySummaryDto
        {
            RunId = r.RunId,
            ForumName = r.Forum != null ? r.Forum.Name : "Unknown",
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt,
            Status = r.Status.ToString(),
            TotalItems = r.TotalItems,
            ProcessedItems = r.ProcessedItems,
            LinkCount = _context.Links.Count(l => l.RunId == r.RunId),
            MatchCount = _context.ScrapeRunItems.Count(i => i.RunId == r.RunId && i.ItemId != null)
        });

        projected = ApplyHistorySort(projected, sortBy, sortDescending);

        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<RunHistorySummaryDto?> GetRunSummaryByIdAsync(int runId, CancellationToken ct = default)
    {
        return await _context.ScrapeRuns.AsNoTracking()
            .Include(r => r.Forum)
            .Where(r => r.RunId == runId)
            .Select(r => new RunHistorySummaryDto
            {
                RunId = r.RunId,
                ForumName = r.Forum != null ? r.Forum.Name : "Unknown",
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Status = r.Status.ToString(),
                TotalItems = r.TotalItems,
                ProcessedItems = r.ProcessedItems,
                LinkCount = _context.Links.Count(l => l.RunId == r.RunId),
                MatchCount = _context.ScrapeRunItems.Count(i => i.RunId == r.RunId && i.ItemId != null)
            })
            .FirstOrDefaultAsync(ct);
    }

    private static IQueryable<RunHistorySummaryDto> ApplyHistorySort(
        IQueryable<RunHistorySummaryDto> query, string? sortBy, bool sortDescending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "forum" => sortDescending ? query.OrderByDescending(r => r.ForumName) : query.OrderBy(r => r.ForumName),
            "items" => sortDescending ? query.OrderByDescending(r => r.TotalItems) : query.OrderBy(r => r.TotalItems),
            "links" => sortDescending ? query.OrderByDescending(r => r.LinkCount) : query.OrderBy(r => r.LinkCount),
            "matches" => sortDescending ? query.OrderByDescending(r => r.MatchCount) : query.OrderBy(r => r.MatchCount),
            "duration" => sortDescending ? query.OrderByDescending(r => r.CompletedAt) : query.OrderBy(r => r.CompletedAt),
            "status" => sortDescending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => sortDescending ? query.OrderByDescending(r => r.StartedAt) : query.OrderBy(r => r.StartedAt)
        };
    }
}
