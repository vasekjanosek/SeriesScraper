namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Status of a data source import run.
/// Stored as string in DB per AC#6.
/// </summary>
public enum ImportRunStatus
{
    Running,
    Complete,
    Failed,
    Partial
}
