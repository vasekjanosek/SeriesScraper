using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Aggregates status data for the main dashboard page.
/// </summary>
public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task TriggerImportAsync(CancellationToken ct = default);
}

/// <summary>
/// Complete dashboard state returned to the UI.
/// </summary>
public record DashboardDto
{
    public IReadOnlyList<ForumStatusDto> Forums { get; init; } = [];
    public ImdbDatasetStatusDto ImdbDataset { get; init; } = new();
    public IReadOnlyList<ActiveRunDto> ActiveRuns { get; init; } = [];
    public WatchlistSummaryDto Watchlist { get; init; } = new();
}

/// <summary>
/// Status of a single configured forum.
/// </summary>
public record ForumStatusDto
{
    public int ForumId { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public bool IsActive { get; init; }
    public ForumConnectivityStatus ConnectivityStatus { get; init; }
    public DateTime? LastSuccessfulScrape { get; init; }
}

/// <summary>
/// IMDB dataset import status for the dashboard.
/// </summary>
public record ImdbDatasetStatusDto
{
    public DateTime? LastImportDate { get; init; }
    public long TitleCount { get; init; }
    public DateTime? NextScheduledRefresh { get; init; }
    public string? ImportStatus { get; init; }
}

/// <summary>
/// Summary of a currently active scrape run for the dashboard.
/// </summary>
public record ActiveRunDto
{
    public int RunId { get; init; }
    public string ForumName { get; init; } = string.Empty;
    public ScrapeRunStatus Status { get; init; }
    public DateTime StartedAt { get; init; }
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int ProgressPercent { get; init; }
}

/// <summary>
/// Watchlist notification summary for the dashboard.
/// </summary>
public record WatchlistSummaryDto
{
    public int UnreadMatchCount { get; init; }
    public int TotalWatchlistItems { get; init; }
}
