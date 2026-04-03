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
    private readonly IForumSessionManager _sessionManager;
    private readonly IForumScraper _forumScraper;
    private readonly ILinkExtractorService _linkExtractor;
    private readonly IUrlValidator _urlValidator;
    private readonly IResponseValidator _responseValidator;
    private readonly ILogger<ForumPostScraper> _logger;

    public ForumPostScraper(
        IForumSessionManager sessionManager,
        IForumScraper forumScraper,
        ILinkExtractorService linkExtractor,
        IUrlValidator urlValidator,
        IResponseValidator responseValidator,
        ILogger<ForumPostScraper> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _forumScraper = forumScraper ?? throw new ArgumentNullException(nameof(forumScraper));
        _linkExtractor = linkExtractor ?? throw new ArgumentNullException(nameof(linkExtractor));
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

            // Extract links from all posts in the thread
            var allLinks = new List<Link>();

            foreach (var post in posts)
            {
                ct.ThrowIfCancellationRequested();

                var links = await _linkExtractor.ExtractLinksAsync(
                    post.HtmlContent, runId, postUrl, ct);
                allLinks.AddRange(links);
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
}
