namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Staging table for IMDB title.akas.tsv bulk import.
/// No indexes, no FK constraints — used only for bulk insert before upsert to live tables.
/// AC#3 from issue #22.
/// </summary>
public class ImdbTitleAkasStaging
{
    public int StagingId { get; set; }
    public required string Tconst { get; set; }
    public int Ordering { get; set; }
    public required string Title { get; set; }
    public string? Region { get; set; }
    public string? Language { get; set; }
    public string? Types { get; set; }
    public string? Attributes { get; set; }
    public bool IsOriginalTitle { get; set; }
}
