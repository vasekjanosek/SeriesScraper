namespace SeriesScraper.Domain.Interfaces;

public interface IResultsService
{
    Task<PagedResult<ResultSummaryDto>> GetResultsAsync(
        ResultFilterDto filter,
        int page,
        int pageSize,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default);

    Task<ResultDetailDto?> GetResultDetailAsync(
        int scrapeRunItemId,
        CancellationToken ct = default);
}

public record ResultSummaryDto
{
    public int RunItemId { get; init; }
    public int RunId { get; init; }
    public required string PostUrl { get; init; }
    public required string Status { get; init; }
    public string? MatchedTitle { get; init; }
    public int? MatchedMediaId { get; init; }
    public string? MediaType { get; init; }
    public decimal? MatchConfidence { get; init; }
    public int? QualityScore { get; init; }
    public int LinkCount { get; init; }
    public DateTime? ProcessedAt { get; init; }
}

public record ResultDetailDto
{
    public int RunItemId { get; init; }
    public int RunId { get; init; }
    public required string PostUrl { get; init; }
    public required string Status { get; init; }
    public DateTime? ProcessedAt { get; init; }

    // IMDB match info
    public string? MatchedTitle { get; init; }
    public int? MatchedMediaId { get; init; }
    public string? MediaType { get; init; }
    public int? Year { get; init; }
    public decimal? ImdbRating { get; init; }
    public int? ImdbVoteCount { get; init; }
    public string? Genres { get; init; }
    public int? EpisodeCount { get; init; }
    public IReadOnlyList<int>? Seasons { get; init; }

    // Links
    public IReadOnlyList<LinkDto> Links { get; init; } = [];
}

public record LinkDto
{
    public int LinkId { get; init; }
    public required string Url { get; init; }
    public required string LinkTypeName { get; init; }
    public int? ParsedSeason { get; init; }
    public int? ParsedEpisode { get; init; }
    public bool IsCurrent { get; init; }
}

public record ResultFilterDto
{
    public int? RunId { get; init; }
    public string? ContentType { get; init; }
    public int? MinQualityScore { get; init; }
    public decimal? MinMatchConfidence { get; init; }
    public string? StatusFilter { get; init; }
    public string? TitleSearch { get; init; }
}

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
