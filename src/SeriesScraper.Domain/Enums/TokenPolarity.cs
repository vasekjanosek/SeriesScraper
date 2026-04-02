namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Polarity of a quality token (positive = better quality, negative = worse quality).
/// Stored as string in database via HasConversion&lt;string&gt;() per ADR-004.
/// </summary>
public enum TokenPolarity
{
    Positive,
    Negative
}
