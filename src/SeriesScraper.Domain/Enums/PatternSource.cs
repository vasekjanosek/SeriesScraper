namespace SeriesScraper.Domain.Enums;

/// <summary>
/// Origin of a quality learned pattern.
/// Stored as string in database via HasConversion&lt;string&gt;() per AC#2.
/// </summary>
public enum PatternSource
{
    Seed,
    Learned,
    User
}
