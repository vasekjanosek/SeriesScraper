using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Tracks a scraping execution for a specific forum.
/// Demonstrates string enum conversion per AC#3.
/// </summary>
public class ScrapeRun
{
    public int RunId { get; set; }
    public int? ForumId { get; set; }
    
    /// <summary>
    /// Denormalized forum name snapshot. Preserved when a forum is deleted
    /// so history records remain meaningful.
    /// </summary>
    public string? ForumName { get; set; }
    
    /// <summary>
    /// Status enum stored as string via HasConversion&lt;string&gt;().
    /// </summary>
    public ScrapeRunStatus Status { get; set; } = ScrapeRunStatus.Pending;
    
    public DateTime StartedAt { get; set; }
    
    /// <summary>
    /// Null = run in-progress or failed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    
    // Navigation properties
    public Forum? Forum { get; set; }
    public ICollection<ScrapeRunItem> Items { get; set; } = new List<ScrapeRunItem>();
    public ICollection<Link> Links { get; set; } = new List<Link>();
}
