using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Result of scraping a single forum post.
/// </summary>
public sealed record PostScrapeResult
{
    public required string PostUrl { get; init; }
    public required IReadOnlyList<Link> ExtractedLinks { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static PostScrapeResult Succeeded(string postUrl, IReadOnlyList<Link> links)
        => new() { PostUrl = postUrl, ExtractedLinks = links, Success = true };

    public static PostScrapeResult Failed(string postUrl, string error)
        => new() { PostUrl = postUrl, ExtractedLinks = Array.Empty<Link>(), Success = false, ErrorMessage = error };
}
