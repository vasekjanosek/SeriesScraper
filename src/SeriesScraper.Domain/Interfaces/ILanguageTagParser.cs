namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Parses language tags from forum thread titles.
/// Thread titles encode language as a slash-separated suffix with comma-separated country codes:
/// "Tenkrát poprvé / Never Have I Ever / CZ, EN"
/// </summary>
public interface ILanguageTagParser
{
    /// <summary>
    /// Extracts ISO 639-1 language codes from a thread title.
    /// Returns empty list if no language tags are detected.
    /// </summary>
    IReadOnlyList<string> ParseLanguageTags(string threadTitle);

    /// <summary>
    /// Returns a comma-separated string of ISO 639-1 codes from the thread title,
    /// or null if no language tags are detected.
    /// </summary>
    string? GetLanguageString(string threadTitle);
}
