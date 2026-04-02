namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Represents a discovered forum section (category/subforum).
/// Demonstrates self-referential FK with OnDelete(Restrict) per AC#4.
/// </summary>
public class ForumSection
{
    public int SectionId { get; set; }
    public int ForumId { get; set; }
    
    /// <summary>
    /// Parent section ID. Null means root section (no parent).
    /// Self-referential FK configured with DeleteBehavior.Restrict.
    /// </summary>
    public int? ParentSectionId { get; set; }
    
    public required string Url { get; set; }
    public required string Name { get; set; }
    
    /// <summary>
    /// ISO 639-1 language code. Null = detection failed or not yet run.
    /// </summary>
    public string? DetectedLanguage { get; set; }
    
    public int? ContentTypeId { get; set; }
    public DateTime? LastCrawledAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public Forum Forum { get; set; } = null!;
    public ForumSection? ParentSection { get; set; }
    public ContentType? ContentType { get; set; }
}
