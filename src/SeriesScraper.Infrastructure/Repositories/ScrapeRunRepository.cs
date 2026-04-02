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
}
