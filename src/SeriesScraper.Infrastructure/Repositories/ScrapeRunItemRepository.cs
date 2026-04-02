using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

public class ScrapeRunItemRepository : IScrapeRunItemRepository
{
    private readonly AppDbContext _context;

    public ScrapeRunItemRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ScrapeRunItem>> GetByRunIdAsync(int runId, CancellationToken ct = default)
    {
        return await _context.Set<ScrapeRunItem>()
            .Where(i => i.RunId == runId)
            .OrderBy(i => i.RunItemId)
            .ToListAsync(ct);
    }

    public async Task<int> CountByRunIdAndStatusAsync(int runId, ScrapeRunItemStatus status, CancellationToken ct = default)
    {
        return await _context.Set<ScrapeRunItem>()
            .CountAsync(i => i.RunId == runId && i.Status == status, ct);
    }
}
