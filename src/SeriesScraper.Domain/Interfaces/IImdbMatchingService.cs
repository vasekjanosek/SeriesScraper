using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// High-level service for matching scraped forum content against IMDB data.
/// Handles title parsing, year extraction, and disambiguation.
/// </summary>
public interface IImdbMatchingService
{
    /// <summary>
    /// Finds the single best IMDB match for a scraped title string.
    /// Automatically extracts embedded year from the title.
    /// Returns null if no match above the minimum confidence threshold.
    /// </summary>
    Task<MetadataSearchResult?> FindBestMatchAsync(
        string scrapedTitle,
        string? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all potential IMDB matches for a scraped title, ranked by confidence.
    /// Automatically extracts embedded year from the title.
    /// </summary>
    Task<IReadOnlyList<MetadataSearchResult>> FindMatchesAsync(
        string scrapedTitle,
        string? type = null,
        CancellationToken cancellationToken = default);
}
