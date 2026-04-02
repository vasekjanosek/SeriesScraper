using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Tracks an individual post URL being processed within a scrape run.
/// post_url is the stable identity during the crawl.
/// </summary>
public class ScrapeRunItem
{
    public int RunItemId { get; set; }
    public int RunId { get; set; }
    public required string PostUrl { get; set; }

    /// <summary>
    /// Nullable FK — backfilled after the scraped item is persisted.
    /// </summary>
    public int? ItemId { get; set; }

    public ScrapeRunItemStatus Status { get; set; } = ScrapeRunItemStatus.Pending;
    public DateTime? ProcessedAt { get; set; }

    // Navigation
    public ScrapeRun Run { get; set; } = null!;
}
