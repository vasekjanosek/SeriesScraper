namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Tracks import runs from external data sources (IMDB, CSFD, etc.).
/// Enables resumable imports by persisting progress and status.
/// AC#6 from issue #22.
/// </summary>
public class DataSourceImportRun
{
    public int ImportRunId { get; set; }
    public int SourceId { get; set; }
    public DateTime StartedAt { get; set; }
    
    /// <summary>
    /// Null = import still running.
    /// </summary>
    public DateTime? FinishedAt { get; set; }
    
    /// <summary>
    /// Status: Running, Complete, Failed, Partial.
    /// Stored as string enum (like MediaType in MediaTitle).
    /// </summary>
    public required string Status { get; set; }
    
    public long RowsImported { get; set; }
    
    /// <summary>
    /// Error message if Status = Failed. Null otherwise.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    // Navigation properties
    public DataSource DataSource { get; set; } = null!;
}
