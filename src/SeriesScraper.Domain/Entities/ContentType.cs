namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Lookup table for content types (TV Series, Movie, Other).
/// Seeded via migration HasData() per AC#6.
/// </summary>
public class ContentType
{
    public int ContentTypeId { get; set; }
    public required string Name { get; set; }
    
    // Navigation properties
    public ICollection<ForumSection> Sections { get; set; } = new List<ForumSection>();
}
