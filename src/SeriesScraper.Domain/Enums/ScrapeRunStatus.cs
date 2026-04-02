namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Status of a scraping run.
/// Stored as string in database via HasConversion&lt;string&gt;() per ADR-004.
/// </summary>
public enum ScrapeRunStatus
{
    Pending,
    Running,
    Partial,
    Complete,
    Failed
}
