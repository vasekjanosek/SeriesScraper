using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically refreshes forum structure (sections) for all active forums.
/// Uses PeriodicTimer per research issue #7.
/// Issue #13: Structure refresh scheduling.
/// </summary>
public class ForumStructureRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ForumStructureRefreshService> _logger;
    internal const string SettingKey = "ForumRefreshIntervalHours";
    internal const int DefaultIntervalHours = 24;

    public ForumStructureRefreshService(
        IServiceProvider serviceProvider,
        ILogger<ForumStructureRefreshService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Forum Structure Refresh Service starting");

        // Run initial refresh on startup
        await RefreshAllForumsAsync(stoppingToken);

        // Then run on configurable schedule
        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalHours = await GetRefreshIntervalAsync(stoppingToken);
            var interval = TimeSpan.FromHours(intervalHours);

            _logger.LogInformation("Next forum structure refresh scheduled in {Interval}", interval);

            using var timer = new PeriodicTimer(interval);

            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RefreshAllForumsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
        }

        _logger.LogInformation("Forum Structure Refresh Service stopping");
    }

    internal async Task RefreshAllForumsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting forum structure refresh for all active forums");

        IReadOnlyList<Forum> forums;
        using (var scope = _serviceProvider.CreateScope())
        {
            var forumRepository = scope.ServiceProvider.GetRequiredService<IForumRepository>();
            forums = await forumRepository.GetActiveAsync(ct);
        }

        _logger.LogInformation("Found {ForumCount} active forums to refresh", forums.Count);

        var newTotal = 0;
        var removedTotal = 0;
        var updatedTotal = 0;
        var failedForums = 0;

        foreach (var forum in forums)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var discoveryService = scope.ServiceProvider
                    .GetRequiredService<IForumSectionDiscoveryService>();
                var sectionRepository = scope.ServiceProvider
                    .GetRequiredService<IForumSectionRepository>();

                // Get existing active sections before refresh
                var existingBefore = await sectionRepository.GetByForumIdAsync(forum.ForumId, ct);
                var existingActiveUrls = existingBefore
                    .Where(s => s.IsActive)
                    .Select(s => s.Url)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Run discovery (handles add/update/deactivate internally)
                var discoveredSections = await discoveryService.DiscoverSectionsAsync(forum, ct);

                // Calculate change deltas for logging
                var discoveredUrls = discoveredSections
                    .Select(s => s.Url)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newSections = discoveredUrls.Except(existingActiveUrls).Count();
                var removedSections = existingActiveUrls.Except(discoveredUrls).Count();
                var updatedSections = discoveredUrls.Intersect(existingActiveUrls).Count();

                newTotal += newSections;
                removedTotal += removedSections;
                updatedTotal += updatedSections;

                _logger.LogInformation(
                    "Forum {ForumId} ({ForumName}) refresh complete: {New} new, {Updated} updated, {Removed} removed sections",
                    forum.ForumId, forum.Name, newSections, updatedSections, removedSections);
            }
            catch (Exception ex)
            {
                failedForums++;
                _logger.LogError(ex,
                    "Forum structure refresh failed for forum {ForumId} ({ForumName}), continuing with remaining forums",
                    forum.ForumId, forum.Name);
                // Continue with other forums — one failure must not stop the rest
            }
        }

        _logger.LogInformation(
            "Forum structure refresh complete: {New} new, {Updated} updated, {Removed} removed sections across {ForumCount} forums ({Failed} failed)",
            newTotal, updatedTotal, removedTotal, forums.Count, failedForums);
    }

    internal async Task<int> GetRefreshIntervalAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingRepository = scope.ServiceProvider
                .GetRequiredService<ISettingRepository>();
            var value = await settingRepository.GetValueAsync(SettingKey, ct);

            if (value != null && int.TryParse(value, out var hours) && hours > 0)
            {
                return hours;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read refresh interval from settings, using default");
        }

        return DefaultIntervalHours;
    }
}
