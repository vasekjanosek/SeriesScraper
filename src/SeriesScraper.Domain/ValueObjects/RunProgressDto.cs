using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Progress summary for a single scrape run.
/// </summary>
public record RunProgressDto
{
    public int RunId { get; init; }
    public int? ForumId { get; init; }
    public string ForumName { get; init; } = string.Empty;
    public ScrapeRunStatus Status { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int CompletedItems { get; init; }
    public int FailedItems { get; init; }
    public int PendingItems { get; init; }
    public string? CurrentItem { get; init; }
    public IReadOnlyList<RunItemProgressDto> Items { get; init; } = Array.Empty<RunItemProgressDto>();
}
