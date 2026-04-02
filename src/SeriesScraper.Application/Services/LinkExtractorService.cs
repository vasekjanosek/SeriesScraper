using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class LinkExtractorService : ILinkExtractorService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum URL length that can be stored in the database.
    /// URLs exceeding this limit are skipped during extraction.
    /// </summary>
    internal const int MaxUrlLength = 2000;

    // Allowed URL schemes per AC#5
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "magnet", "torrent"
    };

    // Extract URLs from href attributes and plain-text URLs
    private static readonly Regex HrefPattern = new(
        @"href\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex PlainUrlPattern = new(
        @"(?:https?|magnet|torrent):[^\s<>""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    // Season/episode parsing patterns
    private static readonly Regex SeasonEpisodePattern = new(
        @"[Ss](\d{1,3})[Ee](\d{1,3})",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex SeasonOnlyPattern = new(
        @"[Ss](?:eason)?[.\-_]?(\d{1,3})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex EpisodeOnlyPattern = new(
        @"(?<![a-zA-Z])[Ee](?:pisode)?[.\-_]?(\d{1,3})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private readonly ILinkTypeService _linkTypeService;
    private readonly ILogger<LinkExtractorService> _logger;

    public LinkExtractorService(ILinkTypeService linkTypeService, ILogger<LinkExtractorService> logger)
    {
        _linkTypeService = linkTypeService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Link>> ExtractLinksAsync(string postHtml, int runId, string postUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(postHtml))
            return Array.Empty<Link>();

        var urls = ExtractUrls(postHtml);
        var linkTypes = await _linkTypeService.GetActiveAsync(ct);
        var links = new List<Link>();

        foreach (var url in urls)
        {
            if (!IsAllowedScheme(url))
                continue;

            if (url.Length > MaxUrlLength)
            {
                _logger.LogWarning("URL exceeds maximum length ({Length} > {Max}), skipping", url.Length, MaxUrlLength);
                continue;
            }

            var linkTypeId = _linkTypeService.ClassifyUrl(url, linkTypes);
            if (linkTypeId is null)
            {
                _logger.LogDebug("URL {Url} did not match any active link type, skipping", url);
                continue;
            }

            var (season, episode) = ParseSeasonEpisode(url);

            links.Add(new Link
            {
                Url = url,
                PostUrl = postUrl,
                LinkTypeId = linkTypeId.Value,
                ParsedSeason = season,
                ParsedEpisode = episode,
                RunId = runId,
                IsCurrent = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        return links;
    }

    internal static IReadOnlyList<string> ExtractUrls(string html)
    {
        var urls = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (Match match in HrefPattern.Matches(html))
            {
                var url = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(url))
                    urls.Add(url);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Safety: href extraction timed out
        }

        try
        {
            foreach (Match match in PlainUrlPattern.Matches(html))
            {
                var url = match.Value.Trim();
                if (!string.IsNullOrEmpty(url))
                    urls.Add(url);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Safety: plain URL extraction timed out
        }

        return urls.ToList();
    }

    internal static bool IsAllowedScheme(string url)
    {
        // Magnet URIs use "magnet:" not "magnet://"
        if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            return true;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return AllowedSchemes.Contains(uri.Scheme);

        return false;
    }

    internal static (int? Season, int? Episode) ParseSeasonEpisode(string url)
    {
        try
        {
            // Try S01E02 pattern first
            var seMatch = SeasonEpisodePattern.Match(url);
            if (seMatch.Success)
            {
                return (
                    int.Parse(seMatch.Groups[1].Value),
                    int.Parse(seMatch.Groups[2].Value)
                );
            }

            // Season-only pattern
            int? season = null;
            var sMatch = SeasonOnlyPattern.Match(url);
            if (sMatch.Success)
                season = int.Parse(sMatch.Groups[1].Value);

            // Episode-only pattern
            int? episode = null;
            var eMatch = EpisodeOnlyPattern.Match(url);
            if (eMatch.Success)
                episode = int.Parse(eMatch.Groups[1].Value);

            return (season, episode);
        }
        catch (RegexMatchTimeoutException)
        {
            return (null, null);
        }
    }
}
