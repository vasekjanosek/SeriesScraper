using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Exceptions;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Scrapes individual forum posts using authenticated sessions.
/// Extracts post content via IForumScraper and links via ILinkExtractorService.
/// Detects session expiry (phpBB2 silently returning login page) and retries once.
/// </summary>
public class ForumPostScraper : IForumPostScraper
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly Regex TitlePattern = new(
        @"<title[^>]*>([^<]+)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private readonly IForumSessionManager _sessionManager;
    private readonly IForumScraper _forumScraper;
    private readonly ILinkExtractorService _linkExtractor;
    private readonly ILanguageTagParser _languageTagParser;
    private readonly IUrlValidator _urlValidator;
    private readonly IResponseValidator _responseValidator;
    private readonly ILogger<ForumPostScraper> _logger;

    public ForumPostScraper(
        IForumSessionManager sessionManager,
        IForumScraper forumScraper,
        ILinkExtractorService linkExtractor,
        ILanguageTagParser languageTagParser,
        IUrlValidator urlValidator,
        IResponseValidator responseValidator,
        ILogger<ForumPostScraper> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _forumScraper = forumScraper ?? throw new ArgumentNullException(nameof(forumScraper));
        _linkExtractor = linkExtractor ?? throw new ArgumentNullException(nameof(linkExtractor));
        _languageTagParser = languageTagParser ?? throw new ArgumentNullException(nameof(languageTagParser));
        _urlValidator = urlValidator ?? throw new ArgumentNullException(nameof(urlValidator));
        _responseValidator = responseValidator ?? throw new ArgumentNullException(nameof(responseValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PostScrapeResult> ScrapePostAsync(
        Forum forum, string postUrl, int runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(forum);
        if (string.IsNullOrWhiteSpace(postUrl))
            return PostScrapeResult.Failed(postUrl ?? string.Empty, "Post URL is null or empty");

        if (!_urlValidator.IsUrlSafe(postUrl, out var reason))
        {
            _logger.LogWarning("Post URL blocked by security policy: {PostUrl} — {Reason}", postUrl, reason);
            return PostScrapeResult.Failed(postUrl, $"URL blocked: {reason}");
        }

        try
        {
            _logger.LogDebug("Scraping post {PostUrl} for forum {ForumId}", postUrl, forum.ForumId);

            // Fetch page and validate session, with one retry on expiry
            var html = await FetchWithSessionValidationAsync(forum, postUrl, ct);

            // Extract post content via IForumScraper
            var posts = await _forumScraper.ExtractPostContentAsync(postUrl, ct);

            if (posts.Count == 0)
            {
                _logger.LogWarning("No post content found at {PostUrl}", postUrl);
                return PostScrapeResult.Failed(postUrl, "No post content found");
            }

            // Parse language tags from the thread title
            var threadTitle = ExtractThreadTitle(html);
            var language = threadTitle is not null
                ? _languageTagParser.GetLanguageString(threadTitle)
                : null;

            if (language is not null)
            {
                _logger.LogDebug(
                    "Detected language '{Language}' from thread title '{Title}' at {PostUrl}",
                    language, threadTitle, postUrl);
            }

            // Extract links from all posts in the thread
            var allLinks = new List<Link>();

            foreach (var post in posts)
            {
                ct.ThrowIfCancellationRequested();

                var links = await _linkExtractor.ExtractLinksAsync(
                    post.HtmlContent, runId, postUrl, ct);
                allLinks.AddRange(links);
            }

            // Apply detected language to all extracted links
            if (language is not null)
            {
                foreach (var link in allLinks)
                {
                    link.Language = language;
                }
            }

            _logger.LogDebug(
                "Scraped {PostCount} posts, extracted {LinkCount} links from {PostUrl}",
                posts.Count, allLinks.Count, postUrl);

            return PostScrapeResult.Succeeded(postUrl, allLinks);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ScrapingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape post {PostUrl}", postUrl);
            return PostScrapeResult.Failed(postUrl, ex.Message);
        }
    }

    /// <summary>
    /// Fetches the page HTML using the authenticated client and validates
    /// that the response is not a login page (session expiry detection).
    /// If session expired, refreshes and retries once.
    /// </summary>
    private async Task<string> FetchWithSessionValidationAsync(
        Forum forum, string postUrl, CancellationToken ct)
    {
        var client = await _sessionManager.GetAuthenticatedClientAsync(forum, ct);
        var html = await client.GetStringAsync(postUrl, ct);

        if (!_responseValidator.IsSessionExpired(html))
            return html;

        // Session expired — refresh and retry once
        _logger.LogWarning(
            "Session expired for forum {ForumId} while fetching {PostUrl} — refreshing session and retrying",
            forum.ForumId, postUrl);

        await _sessionManager.RefreshSessionAsync(forum, ct);
        client = await _sessionManager.GetAuthenticatedClientAsync(forum, ct);
        html = await client.GetStringAsync(postUrl, ct);

        if (_responseValidator.IsSessionExpired(html))
        {
            _logger.LogError(
                "Session still expired for forum {ForumId} after refresh — aborting scrape of {PostUrl}",
                forum.ForumId, postUrl);
            throw new ScrapingException(
                $"Session expired for forum '{forum.Name}' and re-authentication failed. " +
                $"The server returned a login page for URL: {postUrl}");
        }

        return html;
    }

    /// <summary>
    /// Extracts the thread title from the HTML page title element.
    /// phpBB2 titles typically follow: "Forum Name :: View topic - Thread Title"
    /// </summary>
    internal static string? ExtractThreadTitle(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        try
        {
            var match = TitlePattern.Match(html);
            if (!match.Success)
                return null;

            var fullTitle = match.Groups[1].Value.Trim();

            // phpBB2 pattern: "Forum Name :: View topic - Thread Title"
            var viewTopicIndex = fullTitle.IndexOf("View topic -", StringComparison.OrdinalIgnoreCase);
            if (viewTopicIndex >= 0)
            {
                var threadTitle = fullTitle[(viewTopicIndex + "View topic -".Length)..].Trim();
                return string.IsNullOrEmpty(threadTitle) ? null : threadTitle;
            }

            // Generic fallback: remove "::"-separated prefix (common forum pattern)
            var lastSeparator = fullTitle.LastIndexOf("::", StringComparison.Ordinal);
            if (lastSeparator >= 0)
            {
                var threadTitle = fullTitle[(lastSeparator + 2)..].Trim();
                return string.IsNullOrEmpty(threadTitle) ? null : threadTitle;
            }

            return fullTitle;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }
}
