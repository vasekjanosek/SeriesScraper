namespace SeriesScraper.Domain.Entities;

/// <summary>
/// User-configurable link type registry (system + user types).
/// Demonstrates partial index on is_active per AC#1.
/// </summary>
public class LinkType
{
    public int LinkTypeId { get; set; }
    public required string Name { get; set; }
    public required string UrlPattern { get; set; }
    public bool IsSystem { get; set; }
    public string? IconClass { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public ICollection<Link> Links { get; set; } = new List<Link>();
}
