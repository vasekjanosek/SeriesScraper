namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Normalizes media titles for matching and comparison.
/// Handles diacritics removal, year extraction, and fuzzy similarity scoring.
/// </summary>
public interface ITitleNormalizer
{
    /// <summary>
    /// Normalizes a title: lowercase, strip diacritics, remove punctuation, collapse whitespace.
    /// </summary>
    string Normalize(string title);

    /// <summary>
    /// Extracts a four-digit year from parenthesized suffix, e.g. "Movie Name (2024)" → 2024.
    /// Returns null if no year found.
    /// </summary>
    int? ExtractYear(string title);

    /// <summary>
    /// Removes the parenthesized year suffix from a title string.
    /// </summary>
    string StripYear(string title);

    /// <summary>
    /// Computes normalized similarity between two titles (0.0 = completely different, 1.0 = identical).
    /// Both inputs are normalized before comparison.
    /// </summary>
    decimal ComputeSimilarity(string a, string b);
}
