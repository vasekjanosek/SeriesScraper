using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

public class DatabaseStatsProvider : IDatabaseStatsProvider
{
    private readonly AppDbContext _context;

    public DatabaseStatsProvider(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TableRowCount>> GetTableRowCountsAsync(CancellationToken ct = default)
    {
        var counts = new List<TableRowCount>
        {
            new() { TableName = "Forums", RowCount = await _context.Forums.CountAsync(ct) },
            new() { TableName = "MediaTitles", RowCount = await _context.MediaTitles.CountAsync(ct) },
            new() { TableName = "Links", RowCount = await _context.Links.CountAsync(ct) },
            new() { TableName = "ScrapeRuns", RowCount = await _context.ScrapeRuns.CountAsync(ct) },
            new() { TableName = "Settings", RowCount = await _context.Settings.CountAsync(ct) },
            new() { TableName = "LinkTypes", RowCount = await _context.LinkTypes.CountAsync(ct) }
        };

        return counts.AsReadOnly();
    }

    public async Task<bool> CheckConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(ct);
        }
        catch
        {
            return false;
        }
    }
}
