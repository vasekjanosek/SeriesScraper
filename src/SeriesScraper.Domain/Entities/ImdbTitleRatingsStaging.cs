namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Staging table for IMDB title.ratings.tsv bulk import.
/// No indexes, no FK constraints — used only for bulk insert before upsert to live tables.
/// AC#3 from issue #22.
/// </summary>
public class ImdbTitleRatingsStaging
{
    public int StagingId { get; set; }
    public required string Tconst { get; set; }
    public decimal AverageRating { get; set; }
    public int NumVotes { get; set; }
}
