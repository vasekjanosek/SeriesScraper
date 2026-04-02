namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Represents rating information for a media title.
/// </summary>
public sealed record RatingInfo
{
    /// <summary>
    /// The average rating value.
    /// </summary>
    public required decimal Rating { get; init; }

    /// <summary>
    /// The number of votes/ratings cast.
    /// </summary>
    public required int VoteCount { get; init; }
}
