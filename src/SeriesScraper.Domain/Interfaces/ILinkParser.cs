using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for link type parsers. One implementation per link type.
/// Implementations are registered via DI and selected based on URL pattern matching.
/// New parsers are added by implementing this interface and inserting a LinkTypes DB row.
/// </summary>
public interface ILinkParser
{
    /// <summary>
    /// The link type identifier (matches LinkTypes.link_type_id in DB).
    /// </summary>
    int LinkTypeId { get; }

    /// <summary>
    /// Tests whether this parser can handle the given URL.
    /// </summary>
    /// <param name="url">The URL to test.</param>
    /// <returns>True if this parser can classify/parse the URL.</returns>
    bool CanParse(string url);

    /// <summary>
    /// Parses a URL and extracts structured information.
    /// </summary>
    /// <param name="url">The URL to parse.</param>
    /// <returns>Parsed link information including optional season/episode numbers.</returns>
    ParsedLink Parse(string url);
}
