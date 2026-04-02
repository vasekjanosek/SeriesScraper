namespace SeriesScraper.Domain.Interfaces;

public interface IHistoryService
{
    Task<PagedResult<RunHistorySummaryDto>> GetRunHistoryAsync(
        RunHistoryFilterDto filter,
        int page,
        int pageSize,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default);

    Task<RunHistorySummaryDto?> GetRunSummaryAsync(int scrapeRunId, CancellationToken ct = default);
}

public record RunHistorySummaryDto
{
    public int RunId { get; init; }
    public required string ForumName { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    public required string Status { get; init; }
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int LinkCount { get; init; }
    public int MatchCount { get; init; }
}

public record RunHistoryFilterDto
{
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int? ForumId { get; init; }
    public string? StatusFilter { get; init; }
}
