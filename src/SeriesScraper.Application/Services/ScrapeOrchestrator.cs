using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Orchestrates multi-item scraping for a scrape run.
/// For each post URL: scrapes content, extracts links, matches IMDB,
/// persists results, and tracks progress per ScrapeRunItem.
/// Handles per-item failures gracefully — continues processing remaining items.
/// </summary>
public class ScrapeOrchestrator : IScrapeOrchestrator
{
    private readonly IForumPostScraper _postScraper;
    private readonly IForumSearchService _searchService;
    private readonly IForumRepository _forumRepository;
    private readonly IScrapeRunRepository _runRepository;
    private readonly ILinkRepository _linkRepository;
    private readonly IImdbMatchingService _matchingService;
    private readonly ILogger<ScrapeOrchestrator> _logger;

    public ScrapeOrchestrator(
        IForumPostScraper postScraper,
        IForumSearchService searchService,
        IForumRepository forumRepository,
        IScrapeRunRepository runRepository,
        ILinkRepository linkRepository,
        IImdbMatchingService matchingService,
        ILogger<ScrapeOrchestrator> logger)
    {
        _postScraper = postScraper ?? throw new ArgumentNullException(nameof(postScraper));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _forumRepository = forumRepository ?? throw new ArgumentNullException(nameof(forumRepository));
        _runRepository = runRepository ?? throw new ArgumentNullException(nameof(runRepository));
        _linkRepository = linkRepository ?? throw new ArgumentNullException(nameof(linkRepository));
        _matchingService = matchingService ?? throw new ArgumentNullException(nameof(matchingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(ScrapeJob job, CancellationToken ct = default)
    {
        var forum = await _forumRepository.GetByIdAsync(job.ForumId, ct)
            ?? throw new InvalidOperationException($"Forum {job.ForumId} not found");

        _logger.LogInformation(
            "Starting multi-item scrape for run {RunId}, forum {ForumId} ({ForumName})",
            job.RunId, forum.ForumId, forum.Name);

        // Resolve post URLs: explicit list, search criteria, or existing run items
        var postUrls = await ResolvePostUrlsAsync(job, forum, ct);

        if (postUrls.Count == 0)
        {
            _logger.LogWarning("No post URLs to process for run {RunId}", job.RunId);
            return;
        }

        // Filter out already-completed URLs (resume support)
        var skipSet = job.SkipUrls ?? (IReadOnlySet<string>)new HashSet<string>();
        var urlsToProcess = postUrls.Where(u => !skipSet.Contains(u)).ToList();

        _logger.LogInformation(
            "Run {RunId}: {Total} URLs total, {Skip} skipped, {Process} to process",
            job.RunId, postUrls.Count, postUrls.Count - urlsToProcess.Count, urlsToProcess.Count);

        // Update total items on the run
        await UpdateTotalItemsAsync(job.RunId, postUrls.Count, ct);

        var succeeded = 0;
        var failed = 0;

        foreach (var postUrl in urlsToProcess)
        {
            ct.ThrowIfCancellationRequested();

            // Create ScrapeRunItem for tracking
            var runItem = new ScrapeRunItem
            {
                RunId = job.RunId,
                PostUrl = postUrl,
                Status = ScrapeRunItemStatus.Pending
            };
            await _runRepository.AddRunItemAsync(runItem, ct);

            try
            {
                await _runRepository.UpdateRunItemStatusAsync(runItem.RunItemId, ScrapeRunItemStatus.Processing, ct);

                // Scrape the post
                var scrapeResult = await _postScraper.ScrapePostAsync(forum, postUrl, job.RunId, ct);

                if (!scrapeResult.Success)
                {
                    _logger.LogWarning(
                        "Failed to scrape post {PostUrl} in run {RunId}: {Error}",
                        postUrl, job.RunId, scrapeResult.ErrorMessage);
                    await _runRepository.UpdateRunItemStatusAsync(runItem.RunItemId, ScrapeRunItemStatus.Failed, ct);
                    failed++;
                    await _runRepository.IncrementProcessedItemsAsync(job.RunId, ct);
                    continue;
                }

                // Persist extracted links
                if (scrapeResult.ExtractedLinks.Count > 0)
                {
                    await _linkRepository.AccumulateLinksAsync(
                        job.RunId, postUrl, scrapeResult.ExtractedLinks, ct);
                }

                // Attempt IMDB matching from thread title (post URL as proxy)
                await TryMatchImdbAsync(postUrl, ct);

                // Mark item as done
                runItem.ProcessedAt = DateTime.UtcNow;
                await _runRepository.UpdateRunItemStatusAsync(runItem.RunItemId, ScrapeRunItemStatus.Done, ct);
                succeeded++;

                _logger.LogDebug(
                    "Processed post {PostUrl} in run {RunId}: {LinkCount} links extracted",
                    postUrl, job.RunId, scrapeResult.ExtractedLinks.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cancellation requested during processing of {PostUrl}", postUrl);
                await _runRepository.UpdateRunItemStatusAsync(runItem.RunItemId, ScrapeRunItemStatus.Failed, ct);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error processing post {PostUrl} in run {RunId}",
                    postUrl, job.RunId);
                await _runRepository.UpdateRunItemStatusAsync(runItem.RunItemId, ScrapeRunItemStatus.Failed, ct);
                failed++;
            }

            await _runRepository.IncrementProcessedItemsAsync(job.RunId, ct);

            // Politeness delay between items
            if (forum.PolitenessDelayMs > 0)
            {
                await Task.Delay(forum.PolitenessDelayMs, ct);
            }
        }

        _logger.LogInformation(
            "Run {RunId} processing complete: {Succeeded} succeeded, {Failed} failed out of {Total}",
            job.RunId, succeeded, failed, urlsToProcess.Count);
    }

    private async Task<IReadOnlyList<string>> ResolvePostUrlsAsync(
        ScrapeJob job, Forum forum, CancellationToken ct)
    {
        // Priority 1: Explicit post URLs in the job
        if (job.PostUrls is { Count: > 0 })
        {
            _logger.LogDebug("Using {Count} explicit post URLs from job", job.PostUrls.Count);
            return job.PostUrls;
        }

        // Priority 2: Search criteria
        if (job.SearchCriteria is not null)
        {
            _logger.LogDebug("Searching forum for posts matching criteria");
            return await _searchService.SearchPostsAsync(forum, job.SearchCriteria, ct);
        }

        // Priority 3: Load existing run items from DB (resume scenario)
        var run = await _runRepository.GetByIdAsync(job.RunId, ct);
        if (run?.Items.Count > 0)
        {
            var urls = run.Items.Select(i => i.PostUrl).ToList();
            _logger.LogDebug("Loaded {Count} post URLs from existing run items", urls.Count);
            return urls;
        }

        return Array.Empty<string>();
    }

    private async Task UpdateTotalItemsAsync(int runId, int totalItems, CancellationToken ct)
    {
        var run = await _runRepository.GetByIdAsync(runId, ct);
        if (run is not null)
        {
            run.TotalItems = totalItems;
            // TotalItems update is persisted via the repository's tracking
        }
    }

    private async Task TryMatchImdbAsync(string postUrl, CancellationToken ct)
    {
        try
        {
            // Extract a potential title from the post URL path
            var title = ExtractTitleFromUrl(postUrl);
            if (!string.IsNullOrWhiteSpace(title))
            {
                var match = await _matchingService.FindBestMatchAsync(title, cancellationToken: ct);
                if (match is not null)
                {
                    _logger.LogDebug(
                        "IMDB match for '{Title}': {CanonicalTitle} (confidence={Confidence})",
                        title, match.CanonicalTitle, match.ConfidenceScore);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMDB matching failed for post {PostUrl}, continuing", postUrl);
        }
    }

    internal static string? ExtractTitleFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
            if (string.IsNullOrWhiteSpace(lastSegment))
                return null;

            // Replace common URL separators with spaces
            return lastSegment
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Replace('.', ' ')
                .Trim();
        }
        catch
        {
            return null;
        }
    }
}
