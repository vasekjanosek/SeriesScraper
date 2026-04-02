namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Status of an individual item within a scrape run.
/// Stored as string in database via HasConversion&lt;string&gt;() per ADR-004.
/// </summary>
public enum ScrapeRunItemStatus
{
    Pending,
    Processing,
    Done,
    Failed,
    Skipped
}
