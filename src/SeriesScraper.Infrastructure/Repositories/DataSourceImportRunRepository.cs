using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Repositories;

public class DataSourceImportRunRepository : IDataSourceImportRunRepository
{
    private readonly Data.AppDbContext _context;

    public DataSourceImportRunRepository(Data.AppDbContext context)
    {
        _context = context;
    }

    public async Task<DataSourceImportRun?> GetLastImportRunAsync(int sourceId, CancellationToken ct = default)
    {
        return await _context.DataSourceImportRuns
            .Where(r => r.SourceId == sourceId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);
    }
}
