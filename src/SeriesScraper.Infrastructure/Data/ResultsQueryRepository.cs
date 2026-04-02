using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Data;

public class ResultsQueryRepository : IResultsQueryRepository
{
    private readonly AppDbContext _context;

    public ResultsQueryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<ResultSummaryDto> Items, int TotalCount)> GetPagedResultsAsync(
        ResultFilterDto filter,
        int page,
        int pageSize,
        string? sortBy,
        bool sortDescending,
        CancellationToken ct = default)
    {
        var query = from ri in _context.ScrapeRunItems.AsNoTracking()
                    join mt in _context.MediaTitles.AsNoTracking()
                        on ri.ItemId equals mt.MediaId into mtJoin
                    from mt in mtJoin.DefaultIfEmpty()
                    select new { RunItem = ri, MediaTitle = mt };

        // Apply filters
        if (filter.RunId.HasValue)
            query = query.Where(x => x.RunItem.RunId == filter.RunId.Value);

        if (!string.IsNullOrEmpty(filter.StatusFilter) &&
            Enum.TryParse<ScrapeRunItemStatus>(filter.StatusFilter, true, out var statusEnum))
            query = query.Where(x => x.RunItem.Status == statusEnum);

        if (!string.IsNullOrEmpty(filter.ContentType) &&
            Enum.TryParse<MediaType>(filter.ContentType, true, out var mediaTypeEnum))
            query = query.Where(x => x.MediaTitle != null && x.MediaTitle.Type == mediaTypeEnum);

        if (!string.IsNullOrEmpty(filter.TitleSearch))
        {
            var search = filter.TitleSearch.ToLowerInvariant();
            query = query.Where(x =>
                (x.MediaTitle != null && x.MediaTitle.CanonicalTitle.ToLower().Contains(search)) ||
                x.RunItem.PostUrl.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        // Project to DTO with link count sub-query
        var projected = query.Select(x => new ResultSummaryDto
        {
            RunItemId = x.RunItem.RunItemId,
            RunId = x.RunItem.RunId,
            PostUrl = x.RunItem.PostUrl,
            Status = x.RunItem.Status.ToString(),
            MatchedTitle = x.MediaTitle != null ? x.MediaTitle.CanonicalTitle : null,
            MatchedMediaId = x.MediaTitle != null ? x.MediaTitle.MediaId : (int?)null,
            MediaType = x.MediaTitle != null ? x.MediaTitle.Type.ToString() : null,
            MatchConfidence = null, // Confidence is computed at match time, not stored per-item
            QualityScore = null,    // Quality score will be added when quality persistence is implemented
            LinkCount = _context.Links.Count(l =>
                l.RunId == x.RunItem.RunId &&
                l.PostUrl == x.RunItem.PostUrl &&
                l.IsCurrent),
            ProcessedAt = x.RunItem.ProcessedAt
        });

        // Apply sorting
        projected = ApplySorting(projected, sortBy, sortDescending);

        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<ScrapeRunItem?> GetRunItemByIdAsync(int runItemId, CancellationToken ct = default)
    {
        return await _context.ScrapeRunItems
            .AsNoTracking()
            .FirstOrDefaultAsync(ri => ri.RunItemId == runItemId, ct);
    }

    public async Task<IReadOnlyList<Link>> GetLinksForRunItemAsync(
        int runId, string postUrl, CancellationToken ct = default)
    {
        return await _context.Links
            .Include(l => l.LinkType)
            .Where(l => l.RunId == runId && l.PostUrl == postUrl)
            .OrderByDescending(l => l.IsCurrent)
            .ThenByDescending(l => l.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private static IQueryable<ResultSummaryDto> ApplySorting(
        IQueryable<ResultSummaryDto> query,
        string? sortBy,
        bool sortDescending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => sortDescending
                ? query.OrderByDescending(x => x.MatchedTitle)
                : query.OrderBy(x => x.MatchedTitle),
            "status" => sortDescending
                ? query.OrderByDescending(x => x.Status)
                : query.OrderBy(x => x.Status),
            "links" => sortDescending
                ? query.OrderByDescending(x => x.LinkCount)
                : query.OrderBy(x => x.LinkCount),
            "mediatype" => sortDescending
                ? query.OrderByDescending(x => x.MediaType)
                : query.OrderBy(x => x.MediaType),
            "processedat" => sortDescending
                ? query.OrderByDescending(x => x.ProcessedAt)
                : query.OrderBy(x => x.ProcessedAt),
            _ => query.OrderByDescending(x => x.RunItemId) // Default: newest first
        };
    }
}
