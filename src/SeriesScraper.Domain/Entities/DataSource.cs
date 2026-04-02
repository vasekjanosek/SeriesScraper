namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Lookup table for metadata source providers (IMDB, CSFD, etc.).
/// Seeded via migration HasData() per AC#6.
/// </summary>
public class DataSource
{
    public int SourceId { get; set; }
    public required string Name { get; set; }
    
    // Navigation properties
    public ICollection<MediaTitle> MediaTitles { get; set; } = new List<MediaTitle>();
}
