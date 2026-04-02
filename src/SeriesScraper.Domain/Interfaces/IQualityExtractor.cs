using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Extracts quality information from forum post text by evaluating
/// active quality tokens and learned patterns.
/// </summary>
public interface IQualityExtractor
{
    /// <summary>
    /// Scans the post text for quality tokens and learned regex patterns,
    /// selects the best match by rank (with negative polarity downranked),
    /// increments hit counts on matched patterns, and records newly discovered patterns.
    /// </summary>
    /// <param name="postText">The plain-text content of a forum post.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="QualityRank"/> representing the extraction result.</returns>
    Task<QualityRank> ExtractAsync(string postText, CancellationToken ct = default);
}
