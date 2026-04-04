namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Discriminates how a scrape run was initiated.
/// Stored as string in database via HasConversion&lt;string&gt;() per ADR-004.
/// </summary>
public enum ScrapeRunType
{
    Search,
    SingleThread
}
