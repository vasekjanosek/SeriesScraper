namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Staging table for IMDB title.episode.tsv bulk import.
/// No indexes, no FK constraints — used only for bulk insert before upsert to live tables.
/// AC#3 from issue #22.
/// </summary>
public class ImdbTitleEpisodeStaging
{
    public int StagingId { get; set; }
    public required string Tconst { get; set; }
    public required string ParentTconst { get; set; }
    public int? SeasonNumber { get; set; }
    public int? EpisodeNumber { get; set; }
}
