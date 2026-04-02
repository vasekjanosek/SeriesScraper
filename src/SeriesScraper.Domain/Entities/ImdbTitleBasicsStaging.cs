namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Staging table for IMDB title.basics.tsv bulk import.
/// No indexes, no FK constraints — used only for bulk insert before upsert to live tables.
/// AC#3 from issue #22.
/// </summary>
public class ImdbTitleBasicsStaging
{
    public int StagingId { get; set; }
    public required string Tconst { get; set; }
    public required string TitleType { get; set; }
    public required string PrimaryTitle { get; set; }
    public string? OriginalTitle { get; set; }
    public bool IsAdult { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? Genres { get; set; }
}
