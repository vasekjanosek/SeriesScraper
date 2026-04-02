# ADR-002: Interface Contracts — IForumScraper, IMetadataSource, ILinkParser

**Status**: Accepted  
**Date**: 2026-04-02  
**Decision Makers**: Architect Agent  
**Related Issues**: #2, #3, #4, #28

---

## Context

SeriesScraper requires pluggable infrastructure at three integration points:
1. **Forum scraping** — Different forums use different software; the engine must be decoupled from any specific forum implementation.
2. **Metadata sources** — IMDB is the initial source (batch TSV files); CSFD and others will be added later (live HTTP queries). The interface must not assume either data access pattern.
3. **Link parsing** — Link types are database-driven. New link parsers are added by implementing an interface and inserting a DB row.

All interfaces live in `SeriesScraper.Domain.Interfaces`. Implementations live in `SeriesScraper.Infrastructure`.

## Decision

### IForumScraper Contract

**Location**: `SeriesScraper.Domain/Interfaces/IForumScraper.cs`

```csharp
namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for all forum scraper implementations. Each forum software
/// (e.g., vBulletin, phpBB, XenForo) provides a concrete implementation.
/// Session lifecycle (expiry detection, re-authentication) is the responsibility
/// of the concrete implementation.
/// </summary>
public interface IForumScraper
{
    /// <summary>
    /// Authenticates against the forum using the provided credentials.
    /// </summary>
    /// <param name="credentials">Forum credentials (username + password).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authentication succeeded; false otherwise.</returns>
    /// <exception cref="ScrapingException">Thrown on network or protocol errors.</exception>
    Task<bool> AuthenticateAsync(ForumCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current session is still valid.
    /// Implementation-specific: may check cookies, make a probe request, etc.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the session is valid and can be used for requests.</returns>
    Task<bool> ValidateSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates forum sections up to the given depth from the forum index.
    /// Depth 1 = top-level sections only. Depth 2 = top-level + one level of sub-sections.
    /// </summary>
    /// <param name="baseUrl">The forum base URL.</param>
    /// <param name="depth">Crawl depth (default 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Flat list of discovered forum sections with parent references.</returns>
    IAsyncEnumerable<ForumSection> EnumerateSectionsAsync(string baseUrl, int depth = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates threads within a forum section.
    /// </summary>
    /// <param name="sectionUrl">The absolute URL of the forum section.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of thread metadata.</returns>
    IAsyncEnumerable<ForumThread> EnumerateThreadsAsync(string sectionUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the textual content of all posts in a thread.
    /// </summary>
    /// <param name="threadUrl">The absolute URL of the thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of post content in thread order.</returns>
    Task<IReadOnlyList<PostContent>> ExtractPostContentAsync(string threadUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts download links from post content (HTML or text).
    /// </summary>
    /// <param name="postContent">The post content to extract links from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of extracted raw URLs.</returns>
    Task<IReadOnlyList<ExtractedLink>> ExtractLinksAsync(PostContent postContent, CancellationToken cancellationToken = default);
}
```

**Key design decisions:**
- `IAsyncEnumerable<T>` for `EnumerateSections` and `EnumerateThreads` — allows streaming results as they are crawled, without buffering entire section/thread lists in memory.
- `CancellationToken` on every method — supports run cancellation (#16).
- Session lifecycle is the implementation's responsibility — the interface does not dictate cookie storage, session expiry detection, or re-authentication strategy. Each forum software handles this differently.
- Return types are domain DTOs (`ForumSection`, `ForumThread`, `PostContent`, `ExtractedLink`), not HTTP-specific types.

**Supporting domain types** (in `SeriesScraper.Domain.Entities` or `ValueObjects`):
- `ForumCredentials` — Username + password (value object)
- `ForumSection` — Url, Name, ParentUrl (nullable), Depth
- `ForumThread` — Url, Title, PostDate
- `PostContent` — ThreadUrl, PostIndex, HtmlContent, PlainTextContent, PostDate
- `ExtractedLink` — Url, Scheme, LinkText

### IMetadataSource Contract

**Location**: `SeriesScraper.Domain/Interfaces/IMetadataSource.cs`

```csharp
namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for metadata providers. Must support both batch-file sources (IMDB TSV)
/// and live-query sources (CSFD HTTP API) without assuming either data access pattern.
/// All methods return domain-canonical types — never source-specific identifiers.
/// </summary>
public interface IMetadataSource
{
    /// <summary>
    /// The unique identifier for this metadata source (matches DataSources.source_id in DB).
    /// </summary>
    string SourceIdentifier { get; }

    /// <summary>
    /// Searches for a media title by name with optional filters.
    /// </summary>
    /// <param name="query">Title name to search for.</param>
    /// <param name="year">Optional release year filter.</param>
    /// <param name="type">Optional content type filter (movie, series, episode).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered list of matches with confidence scores (best match first).</returns>
    Task<IReadOnlyList<MetadataSearchResult>> SearchByTitleAsync(
        string query,
        int? year = null,
        string? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a media entry by its source-specific external identifier.
    /// For IMDB: tconst. For CSFD: CSFD numeric ID. The caller uses this for
    /// re-resolution, not for cross-source lookups.
    /// </summary>
    /// <param name="externalId">The source-specific identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The metadata result, or null if not found.</returns>
    Task<MetadataSearchResult?> SearchByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the episode list for a series.
    /// </summary>
    /// <param name="titleId">Canonical media_id (not source-specific ID).</param>
    /// <param name="season">Optional season number filter. Null = all seasons.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of episodes.</returns>
    Task<IReadOnlyList<EpisodeInfo>> GetEpisodeListAsync(
        int titleId,
        int? season = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves rating information for a title.
    /// </summary>
    /// <param name="titleId">Canonical media_id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rating info, or null if no rating available.</returns>
    Task<RatingInfo?> GetRatingsAsync(int titleId, CancellationToken cancellationToken = default);
}
```

**Key design decisions:**
- `SourceIdentifier` property — Enables runtime resolution. DI registers multiple `IMetadataSource` implementations; the application selects by source ID.
- Methods accept `int titleId` (canonical `media_id`) for lookups, not source-specific IDs (tconst) — enforces the canonical layer (#21).
- `SearchByExternalIdAsync` exists for re-resolution within the same source (e.g., IMDB re-import), but cross-source lookups go through `media_id`.
- Return types are domain DTOs (`MetadataSearchResult`, `EpisodeInfo`, `RatingInfo`), not IMDB-specific structures.

**Supporting domain types**:
- `MetadataSearchResult` — MediaId (nullable if new), CanonicalTitle, Year, Type, ConfidenceScore, ExternalId
- `EpisodeInfo` — Season, EpisodeNumber, Title (nullable)
- `RatingInfo` — Rating (decimal), VoteCount (int)

### ILinkParser Contract

**Location**: `SeriesScraper.Domain/Interfaces/ILinkParser.cs`

```csharp
namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Contract for link type parsers. One implementation per link type.
/// Implementations are registered via DI and selected based on URL pattern matching.
/// New parsers are added by implementing this interface and inserting a LinkTypes DB row.
/// </summary>
public interface ILinkParser
{
    /// <summary>
    /// The link type identifier (matches LinkTypes.link_type_id in DB).
    /// </summary>
    int LinkTypeId { get; }

    /// <summary>
    /// Tests whether this parser can handle the given URL.
    /// </summary>
    /// <param name="url">The URL to test.</param>
    /// <returns>True if this parser can classify/parse the URL.</returns>
    bool CanParse(string url);

    /// <summary>
    /// Parses a URL and extracts structured information.
    /// </summary>
    /// <param name="url">The URL to parse.</param>
    /// <returns>Parsed link information including optional season/episode numbers.</returns>
    ParsedLink Parse(string url);
}
```

**Supporting domain types**:
- `ParsedLink` — Url, LinkTypeId, ParsedSeason (nullable int), ParsedEpisode (nullable int), Scheme

## Alternatives Considered

### Alternative: Single IExternalService interface for both scraping and metadata

Rejected because scraping and metadata are fundamentally different concerns:
- Scraping is stateful (session, cookies, authentication lifecycle)
- Metadata is stateless (query-response)
- Scraping is forum-specific; metadata is data-source-specific
- Different error handling needs (network errors vs data not found)

### Alternative: Generic `IPlugin<T>` base interface

Rejected because:
- Over-abstract — the three integration points have completely different method signatures
- Would require casting or type-checking at runtime, losing compile-time safety
- No shared behavior to extract into a base interface

## Consequences

- Every feature that touches forum interaction depends only on `IForumScraper`
- Every feature that touches metadata depends only on `IMetadataSource`
- New forum software support = new `IForumScraper` implementation + DI registration
- New metadata source = new `IMetadataSource` implementation + `DataSources` row + DI registration
- New link type = new `ILinkParser` implementation + `LinkTypes` row + DI registration
- Zero existing code changes required for any of the above extensions
