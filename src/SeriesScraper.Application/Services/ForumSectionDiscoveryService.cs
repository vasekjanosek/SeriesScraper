using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using ForumSectionVO = SeriesScraper.Domain.ValueObjects.ForumSection;

namespace SeriesScraper.Application.Services;

public class ForumSectionDiscoveryService : IForumSectionDiscoveryService
{
    // Seeded ContentType IDs from ContentTypeConfiguration
    private const int ContentTypeTvSeries = 1;
    private const int ContentTypeMovie = 2;
    private const int ContentTypeOther = 3;

    private static readonly Regex TvSeriesPattern = new(
        @"\b(series|tv|seriály?|shows?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex MoviePattern = new(
        @"\b(movies?|films?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private readonly IForumScraper _forumScraper;
    private readonly IForumSectionRepository _repository;
    private readonly ILanguageDetector _languageDetector;
    private readonly ILogger<ForumSectionDiscoveryService> _logger;

    public ForumSectionDiscoveryService(
        IForumScraper forumScraper,
        IForumSectionRepository repository,
        ILanguageDetector languageDetector,
        ILogger<ForumSectionDiscoveryService> logger)
    {
        _forumScraper = forumScraper;
        _repository = repository;
        _languageDetector = languageDetector;
        _logger = logger;
    }

    internal static int ClassifyContentType(string sectionName)
    {
        if (TvSeriesPattern.IsMatch(sectionName))
            return ContentTypeTvSeries;
        if (MoviePattern.IsMatch(sectionName))
            return ContentTypeMovie;
        return ContentTypeOther;
    }

    public async Task<IReadOnlyList<ForumSection>> DiscoverSectionsAsync(
        Forum forum, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(forum);

        _logger.LogInformation(
            "Starting section discovery for forum {ForumId} ({ForumName}) at {BaseUrl}",
            forum.ForumId, forum.Name, forum.BaseUrl);

        // 1. Enumerate sections via IForumScraper
        var discoveredVOs = new List<ForumSectionVO>();
        await foreach (var section in _forumScraper.EnumerateSectionsAsync(
            forum.BaseUrl, forum.CrawlDepth, ct))
        {
            discoveredVOs.Add(section);
        }

        _logger.LogInformation(
            "Discovered {Count} sections for forum {ForumId}",
            discoveredVOs.Count, forum.ForumId);

        // 2. Get existing sections from DB
        var existingSections = await _repository.GetByForumIdAsync(forum.ForumId, ct);
        var existingByUrl = existingSections.ToDictionary(
            s => s.Url, StringComparer.OrdinalIgnoreCase);

        // 3. Process discovered sections (add new, update existing)
        var result = new List<ForumSection>();
        var discoveredUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newSectionsByUrl = new Dictionary<string, ForumSection>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var vo in discoveredVOs)
        {
            discoveredUrls.Add(vo.Url);
            var detectedLanguage = _languageDetector.DetectLanguage(vo.Name);

            var contentTypeId = ClassifyContentType(vo.Name);

            if (existingByUrl.TryGetValue(vo.Url, out var existing))
            {
                // Update existing section
                existing.Name = vo.Name;
                existing.IsActive = true;
                existing.DetectedLanguage = detectedLanguage;
                existing.ContentTypeId = contentTypeId;
                existing.LastCrawledAt = DateTime.UtcNow;
                await _repository.UpdateAsync(existing, ct);
                result.Add(existing);
                newSectionsByUrl[vo.Url] = existing;

                _logger.LogDebug("Updated section {Url} for forum {ForumId}",
                    vo.Url, forum.ForumId);
            }
            else
            {
                // Resolve parent section
                int? parentSectionId = null;
                if (vo.ParentUrl != null)
                {
                    if (existingByUrl.TryGetValue(vo.ParentUrl, out var parent))
                        parentSectionId = parent.SectionId;
                    else if (newSectionsByUrl.TryGetValue(vo.ParentUrl, out var newParent))
                        parentSectionId = newParent.SectionId;
                }

                var newSection = new ForumSection
                {
                    ForumId = forum.ForumId,
                    Url = vo.Url,
                    Name = vo.Name,
                    ParentSectionId = parentSectionId,
                    DetectedLanguage = detectedLanguage,
                    ContentTypeId = contentTypeId,
                    IsActive = true,
                    LastCrawledAt = DateTime.UtcNow
                };

                newSection = await _repository.AddAsync(newSection, ct);
                result.Add(newSection);
                newSectionsByUrl[vo.Url] = newSection;

                _logger.LogDebug("Added new section {Url} for forum {ForumId}",
                    vo.Url, forum.ForumId);
            }
        }

        // 4. Mark sections not discovered as inactive
        foreach (var existing in existingSections)
        {
            if (!discoveredUrls.Contains(existing.Url) && existing.IsActive)
            {
                existing.IsActive = false;
                await _repository.UpdateAsync(existing, ct);

                _logger.LogInformation(
                    "Marked section {Url} as inactive for forum {ForumId}",
                    existing.Url, forum.ForumId);
            }
        }

        _logger.LogInformation(
            "Section discovery complete for forum {ForumId}: {Total} total active sections",
            forum.ForumId, result.Count);

        return result.AsReadOnly();
    }
}
