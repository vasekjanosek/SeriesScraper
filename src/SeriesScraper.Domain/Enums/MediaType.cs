namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Type of media title.
/// Stored as string in database via HasConversion&lt;string&gt;() per ADR-004.
/// </summary>
public enum MediaType
{
    Movie,
    Series,
    Episode
}
