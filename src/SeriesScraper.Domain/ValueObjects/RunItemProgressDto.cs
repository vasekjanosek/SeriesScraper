using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Progress detail for a single item within a scrape run.
/// </summary>
public record RunItemProgressDto
{
    public int RunItemId { get; init; }
    public string PostUrl { get; init; } = string.Empty;
    public ScrapeRunItemStatus Status { get; init; }
    public DateTime? ProcessedAt { get; init; }
}
