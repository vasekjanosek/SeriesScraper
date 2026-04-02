namespace SeriesScraper.Domain.Interfaces;

public interface IDatabaseStatsProvider
{
    Task<IReadOnlyList<TableRowCount>> GetTableRowCountsAsync(CancellationToken ct = default);
    Task<bool> CheckConnectionAsync(CancellationToken ct = default);
}
