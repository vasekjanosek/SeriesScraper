using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Data;

public class LinkRepository : ILinkRepository
{
    private readonly AppDbContext _context;

    public LinkRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Link>> GetCurrentByRunIdAsync(int runId, CancellationToken ct = default)
    {
        return await _context.Links
            .Where(l => l.RunId == runId && l.IsCurrent)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Link>> GetCurrentByPostUrlAsync(string postUrl, int runId, CancellationToken ct = default)
    {
        return await _context.Links
            .Where(l => l.PostUrl == postUrl && l.RunId == runId && l.IsCurrent)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<Link> links, CancellationToken ct = default)
    {
        _context.Links.AddRange(links);
        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkPreviousAsNonCurrentAsync(int runId, string postUrl, CancellationToken ct = default)
    {
        var links = await _context.Links
            .Where(l => l.RunId == runId && l.PostUrl == postUrl && l.IsCurrent)
            .ToListAsync(ct);

        foreach (var link in links)
            link.IsCurrent = false;

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Accumulate-with-flag pattern per AC#7:
    /// Mark existing links as non-current and insert new links atomically.
    /// SaveChangesAsync wraps all pending changes in a single database transaction.
    /// </summary>
    public async Task AccumulateLinksAsync(int runId, string postUrl, IEnumerable<Link> newLinks, CancellationToken ct = default)
    {
        // Mark existing links for this post as non-current
        var existing = await _context.Links
            .Where(l => l.RunId == runId && l.PostUrl == postUrl && l.IsCurrent)
            .ToListAsync(ct);

        foreach (var link in existing)
            link.IsCurrent = false;

        // Insert new links with is_current = true
        _context.Links.AddRange(newLinks);

        // Single SaveChangesAsync = single transaction (atomic)
        await _context.SaveChangesAsync(ct);
    }
}
