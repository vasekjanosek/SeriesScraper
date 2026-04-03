using System.Text.RegularExpressions;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Parses language tags from forum thread titles.
/// Expects the pattern: "Title / AlternateTitle / CZ, EN"
/// where the last slash-separated segment contains comma-separated country-style codes.
/// Maps country codes to ISO 639-1 language codes.
/// </summary>
public class LanguageTagParser : ILanguageTagParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Known country-code to ISO 639-1 mappings.
    /// </summary>
    private static readonly Dictionary<string, string> CountryToIso = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CZ"] = "cs",
        ["SK"] = "sk",
        ["EN"] = "en",
        ["DE"] = "de",
        ["FR"] = "fr",
        ["ES"] = "es",
        ["IT"] = "it",
        ["PL"] = "pl",
        ["HU"] = "hu",
        ["RU"] = "ru",
        ["PT"] = "pt",
        ["NL"] = "nl",
        ["RO"] = "ro",
        ["HR"] = "hr",
        ["SR"] = "sr",
        ["BG"] = "bg",
        ["UA"] = "uk",
        ["JP"] = "ja",
        ["KR"] = "ko",
        ["CN"] = "zh",
    };

    // Matches country-style codes: 2-3 uppercase letters
    private static readonly Regex CountryCodePattern = new(
        @"^[A-Za-z]{2,3}$",
        RegexOptions.Compiled,
        RegexTimeout);

    public IReadOnlyList<string> ParseLanguageTags(string threadTitle)
    {
        if (string.IsNullOrWhiteSpace(threadTitle))
            return Array.Empty<string>();

        // Split by '/' and take the last segment
        var segments = threadTitle.Split('/');
        if (segments.Length < 2)
            return Array.Empty<string>();

        var lastSegment = segments[^1].Trim();
        if (string.IsNullOrEmpty(lastSegment))
            return Array.Empty<string>();

        // Split by ',' to get individual codes
        var codes = lastSegment.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var result = new List<string>();
        foreach (var code in codes)
        {
            if (!IsValidCountryCode(code))
                return Array.Empty<string>(); // If any token isn't a code, this isn't a language segment

            var iso = MapToIso(code);
            if (!result.Contains(iso, StringComparer.Ordinal))
                result.Add(iso);
        }

        return result;
    }

    public string? GetLanguageString(string threadTitle)
    {
        var tags = ParseLanguageTags(threadTitle);
        return tags.Count > 0 ? string.Join(",", tags) : null;
    }

    private static bool IsValidCountryCode(string code)
    {
        try
        {
            return CountryCodePattern.IsMatch(code);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string MapToIso(string countryCode)
    {
        return CountryToIso.TryGetValue(countryCode, out var iso)
            ? iso
            : countryCode.ToLowerInvariant();
    }
}
