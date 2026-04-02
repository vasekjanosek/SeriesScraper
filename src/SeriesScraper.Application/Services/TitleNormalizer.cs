using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Normalizes media titles for matching: strips diacritics, removes punctuation,
/// extracts embedded years, and computes Levenshtein-based similarity scores.
/// </summary>
public class TitleNormalizer : ITitleNormalizer
{
    private static readonly Regex YearPattern = new(@"\((\d{4})\)", RegexOptions.Compiled);
    private static readonly Regex YearStripPattern = new(@"\s*\(\d{4}\)\s*", RegexOptions.Compiled);
    private static readonly Regex NonAlphanumericPattern = new(@"[^\p{L}\p{N}\s]", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    public string Normalize(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var stripped = StripYear(title);
        var noDiacritics = RemoveDiacritics(stripped);
        var lower = noDiacritics.ToLowerInvariant();
        var noPunctuation = NonAlphanumericPattern.Replace(lower, "");
        var collapsed = WhitespacePattern.Replace(noPunctuation, " ").Trim();

        return collapsed;
    }

    public int? ExtractYear(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var match = YearPattern.Match(title);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
            return year;

        return null;
    }

    public string StripYear(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        return YearStripPattern.Replace(title, " ").Trim();
    }

    public decimal ComputeSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            return 1.0m;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0.0m;

        var na = Normalize(a);
        var nb = Normalize(b);

        if (na.Length == 0 && nb.Length == 0)
            return 1.0m;
        if (na.Length == 0 || nb.Length == 0)
            return 0.0m;

        if (na == nb)
            return 1.0m;

        var distance = LevenshteinDistance(na, nb);
        var maxLen = Math.Max(na.Length, nb.Length);

        return 1.0m - (decimal)distance / maxLen;
    }

    internal static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    internal static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var costs = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            costs[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            costs[0] = i;
            var prev = i - 1;

            for (var j = 1; j <= b.Length; j++)
            {
                var temp = costs[j];
                costs[j] = a[i - 1] == b[j - 1]
                    ? prev
                    : Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), prev + 1);
                prev = temp;
            }
        }

        return costs[b.Length];
    }
}
