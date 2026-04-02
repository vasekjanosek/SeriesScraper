namespace SeriesScraper.Domain.Interfaces;

public interface IAppInfoService
{
    Task<DatabaseStatsDto> GetDatabaseStatsAsync(CancellationToken ct = default);
    Task<AppInfoDto> GetAppInfoAsync(CancellationToken ct = default);
}

public record DatabaseStatsDto
{
    public IReadOnlyList<TableRowCount> TableCounts { get; init; } = [];
}

public record TableRowCount
{
    public required string TableName { get; init; }
    public long RowCount { get; init; }
}

public record AppInfoDto
{
    public required string Version { get; init; }
    public TimeSpan Uptime { get; init; }
    public DatabaseStatsDto DatabaseStats { get; init; } = new();
}
