using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Manages scrape run lifecycle: creation, status transitions, enqueueing,
/// job processing, and startup recovery.
/// </summary>
public class ScrapeRunService : IScrapeRunService
{
    private readonly IScrapeRunRepository _repository;
    private readonly IScrapingJobQueue _jobQueue;
    private readonly IScrapeOrchestrator _orchestrator;
    private readonly IForumRepository _forumRepository;
    private readonly IUrlValidator _urlValidator;
    private readonly ILogger<ScrapeRunService> _logger;

    public ScrapeRunService(
        IScrapeRunRepository repository,
        IScrapingJobQueue jobQueue,
        IScrapeOrchestrator orchestrator,
        IForumRepository forumRepository,
        IUrlValidator urlValidator,
        ILogger<ScrapeRunService> logger)
    {
        _repository = repository;
        _jobQueue = jobQueue;
        _orchestrator = orchestrator;
        _forumRepository = forumRepository;
        _urlValidator = urlValidator;
        _logger = logger;
    }

    public async Task<ScrapeRun> CreateRunAsync(int forumId, CancellationToken ct = default)
    {
        var run = new ScrapeRun
        {
            ForumId = forumId,
            Status = ScrapeRunStatus.Pending,
            StartedAt = DateTime.UtcNow
        };

        run = await _repository.CreateAsync(run, ct);
        _logger.LogInformation("Created scrape run {RunId} for forum {ForumId}", run.RunId, forumId);
        return run;
    }

    public async Task EnqueueRunAsync(int forumId, IReadOnlySet<string>? skipUrls = null, CancellationToken ct = default)
    {
        var run = await CreateRunAsync(forumId, ct);
        var job = new ScrapeJob
        {
            RunId = run.RunId,
            ForumId = forumId,
            SkipUrls = skipUrls
        };
        await _jobQueue.EnqueueAsync(job, ct);
        _logger.LogInformation("Enqueued scrape run {RunId} for forum {ForumId}", run.RunId, forumId);
    }

    public async Task<ScrapeRun> ScrapeByUrlAsync(string threadUrl, int forumId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadUrl))
            throw new ArgumentException("Thread URL is required.", nameof(threadUrl));

        // SSRF check via existing IUrlValidator
        if (!_urlValidator.IsUrlSafe(threadUrl, out var reason))
            throw new ArgumentException($"URL blocked: {reason}", nameof(threadUrl));

        // Must be a viewtopic.php URL
        if (!Uri.TryCreate(threadUrl, UriKind.Absolute, out var parsedUri)
            || !parsedUri.AbsolutePath.Contains("viewtopic.php", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("URL must be a viewtopic.php thread URL.", nameof(threadUrl));
        }

        // Validate URL matches the forum's base URL
        var forum = await _forumRepository.GetByIdAsync(forumId, ct)
            ?? throw new InvalidOperationException($"Forum {forumId} not found.");

        if (!Uri.TryCreate(forum.BaseUrl, UriKind.Absolute, out var baseUri)
            || !string.Equals(parsedUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"URL host '{parsedUri.Host}' does not match forum base URL '{forum.BaseUrl}'.",
                nameof(threadUrl));
        }

        // Create a run with SingleThread type
        var run = new ScrapeRun
        {
            ForumId = forumId,
            Status = ScrapeRunStatus.Pending,
            RunType = ScrapeRunType.SingleThread,
            StartedAt = DateTime.UtcNow
        };

        run = await _repository.CreateAsync(run, ct);

        // Enqueue with the single URL as PostUrls
        var job = new ScrapeJob
        {
            RunId = run.RunId,
            ForumId = forumId,
            PostUrls = new[] { threadUrl }
        };

        await _jobQueue.EnqueueAsync(job, ct);
        _logger.LogInformation(
            "Enqueued single-thread scrape run {RunId} for forum {ForumId}, URL: {Url}",
            run.RunId, forumId, threadUrl);

        return run;
    }

    public async Task ProcessJobAsync(ScrapeJob job, CancellationToken ct = default)
    {
        await _repository.UpdateStatusAsync(job.RunId, ScrapeRunStatus.Running, ct: ct);
        _logger.LogInformation("Scrape run {RunId} started processing for forum {ForumId}", job.RunId, job.ForumId);

        // Check for cancellation before doing work
        ct.ThrowIfCancellationRequested();

        // Delegate to IScrapeOrchestrator for multi-item scraping pipeline
        await _orchestrator.ExecuteAsync(job, ct);

        await CompleteRunAsync(job.RunId, ct);
    }

    public async Task CompleteRunAsync(int runId, CancellationToken ct = default)
    {
        await _repository.UpdateStatusAsync(runId, ScrapeRunStatus.Complete, DateTime.UtcNow, ct);
        _logger.LogInformation("Scrape run {RunId} completed", runId);
    }

    public async Task FailRunAsync(int runId, CancellationToken ct = default)
    {
        await _repository.UpdateStatusAsync(runId, ScrapeRunStatus.Failed, DateTime.UtcNow, ct);
        _logger.LogWarning("Scrape run {RunId} failed", runId);
    }

    public async Task MarkRunAsPartialAsync(int runId, CancellationToken ct = default)
    {
        await _repository.UpdateStatusAsync(runId, ScrapeRunStatus.Partial, DateTime.UtcNow, ct);
        _logger.LogInformation("Scrape run {RunId} marked as partial", runId);
    }

    public async Task MarkInterruptedRunsAsPartialAsync(CancellationToken ct = default)
    {
        await _repository.MarkRunningAsPartialAsync(ct);
        _logger.LogInformation("Marked interrupted runs as Partial on startup");
    }

    public async Task<ScrapeRun?> GetRunAsync(int runId, CancellationToken ct = default)
    {
        return await _repository.GetByIdAsync(runId, ct);
    }

    public Task CancelRunAsync(int runId)
    {
        var cancelled = _jobQueue.CancelRun(runId);
        if (cancelled)
            _logger.LogInformation("Scrape run {RunId} cancellation requested", runId);
        else
            _logger.LogWarning("Scrape run {RunId} not found in active jobs for cancellation", runId);

        return Task.CompletedTask;
    }
}
