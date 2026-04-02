using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Searches forum sections for posts matching given criteria.
/// Enumerates threads via IForumScraper and filters by title query.
/// </summary>
public class ForumSearchService : IForumSearchService
{
    private readonly IForumScraper _forumScraper;
    private readonly IForumSessionManager _sessionManager;
    private readonly IForumSectionRepository _sectionRepository;
    private readonly IUrlValidator _urlValidator;
    private readonly ILogger<ForumSearchService> _logger;

    public ForumSearchService(
        IForumScraper forumScraper,
        IForumSessionManager sessionManager,
        IForumSectionRepository sectionRepository,
        IUrlValidator urlValidator,
        ILogger<ForumSearchService> logger)
    {
        _forumScraper = forumScraper ?? throw new ArgumentNullException(nameof(forumScraper));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _sectionRepository = sectionRepository ?? throw new ArgumentNullException(nameof(sectionRepository));
        _urlValidator = urlValidator ?? throw new ArgumentNullException(nameof(urlValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<string>> SearchPostsAsync(
        Forum forum, ForumSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(forum);
        ArgumentNullException.ThrowIfNull(criteria);

        _logger.LogInformation(
            "Searching forum {ForumId} ({ForumName}) with query '{Query}', section '{Section}', max {Max}",
            forum.ForumId, forum.Name, criteria.TitleQuery, criteria.SectionUrl, criteria.MaxResults);

        // Ensure authenticated session
        await _sessionManager.GetAuthenticatedClientAsync(forum, ct);

        // Determine which sections to search
        var sectionUrls = await ResolveSectionUrlsAsync(forum, criteria, ct);

        if (sectionUrls.Count == 0)
        {
            _logger.LogWarning("No sections found to search for forum {ForumId}", forum.ForumId);
            return Array.Empty<string>();
        }

        var results = new List<string>();

        foreach (var sectionUrl in sectionUrls)
        {
            if (results.Count >= criteria.MaxResults)
                break;

            ct.ThrowIfCancellationRequested();

            if (!_urlValidator.IsUrlSafe(sectionUrl, out var sectionReason))
            {
                _logger.LogWarning("Section URL blocked by security policy: {SectionUrl} — {Reason}", sectionUrl, sectionReason);
                continue;
            }

            try
            {
                await foreach (var thread in _forumScraper.EnumerateThreadsAsync(sectionUrl, ct))
                {
                    if (results.Count >= criteria.MaxResults)
                        break;

                    if (!_urlValidator.IsUrlSafe(thread.Url))
                    {
                        _logger.LogDebug("Thread URL blocked by security policy: {ThreadUrl}", thread.Url);
                        continue;
                    }

                    if (MatchesCriteria(thread, criteria))
                    {
                        results.Add(thread.Url);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error enumerating threads in section {SectionUrl}, continuing with next section",
                    sectionUrl);
            }
        }

        _logger.LogInformation(
            "Search found {Count} matching posts in forum {ForumId}",
            results.Count, forum.ForumId);

        return results;
    }

    private async Task<IReadOnlyList<string>> ResolveSectionUrlsAsync(
        Forum forum, ForumSearchCriteria criteria, CancellationToken ct)
    {
        // If specific section URL provided, use it
        if (!string.IsNullOrWhiteSpace(criteria.SectionUrl))
        {
            return new[] { criteria.SectionUrl };
        }

        // Otherwise, get all active sections for the forum from DB
        var sections = await _sectionRepository.GetByForumIdAsync(forum.ForumId, ct);
        return sections
            .Where(s => s.IsActive)
            .Select(s => s.Url)
            .ToList();
    }

    internal static bool MatchesCriteria(ForumThread thread, ForumSearchCriteria criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria.TitleQuery))
            return true;

        return thread.Title.Contains(criteria.TitleQuery, StringComparison.OrdinalIgnoreCase);
    }
}
