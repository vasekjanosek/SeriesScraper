using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Scrapes individual forum posts using authenticated sessions.
/// Extracts post content via IForumScraper and links via ILinkExtractorService.
/// </summary>
public class ForumPostScraper : IForumPostScraper
{
    private readonly IForumSessionManager _sessionManager;
    private readonly IForumScraper _forumScraper;
    private readonly ILinkExtractorService _linkExtractor;
    private readonly ILogger<ForumPostScraper> _logger;

    public ForumPostScraper(
        IForumSessionManager sessionManager,
        IForumScraper forumScraper,
        ILinkExtractorService linkExtractor,
        ILogger<ForumPostScraper> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _forumScraper = forumScraper ?? throw new ArgumentNullException(nameof(forumScraper));
        _linkExtractor = linkExtractor ?? throw new ArgumentNullException(nameof(linkExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PostScrapeResult> ScrapePostAsync(
        Forum forum, string postUrl, int runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(forum);
        if (string.IsNullOrWhiteSpace(postUrl))
            return PostScrapeResult.Failed(postUrl ?? string.Empty, "Post URL is null or empty");

        try
        {
            _logger.LogDebug("Scraping post {PostUrl} for forum {ForumId}", postUrl, forum.ForumId);

            // Ensure authenticated session
            await _sessionManager.GetAuthenticatedClientAsync(forum, ct);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape post {PostUrl}", postUrl);
            return PostScrapeResult.Failed(postUrl, ex.Message);
        }
    }
}
