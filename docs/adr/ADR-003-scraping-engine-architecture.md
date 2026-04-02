# ADR-003: Scraping Engine Architecture — Background Services and Job Queue

**Status**: Accepted  
**Date**: 2026-04-02  
**Decision Makers**: Architect Agent  
**Related Issues**: #14, #15, #16, #17, #32

---

## Context

The scraping engine must:
- Run scrape jobs in the background, never blocking HTTP request threads
- Support cancellation and partial run resume
- Push real-time progress updates to the Blazor UI
- Enforce per-forum politeness delays and global concurrency limits
- Handle session expiry and automatic re-authentication

## Decision

### Scraping Pipeline Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│ Blazor UI                                                        │
│                                                                  │
│  Search Form ──→ IScrapingJobQueue.EnqueueAsync(ScrapeRequest)  │
│                                                                  │
│  Run Progress ←── SignalR Hub ←── IRunStatusService              │
└──────────────────────────────────────────────────────────────────┘
                           │                    ▲
                     Channel<T>                 │ (writes status)
                           │                    │
┌──────────────────────────▼────────────────────┤──────────────────┐
│ BackgroundService (thin shell in Web project)                    │
│                                                                  │
│  Delegates to: IScrapingWorker.ExecuteAsync(ScrapeRequest, ct)  │
│                                                                  │
│  IScrapingWorker pipeline per item:                              │
│    1. ValidateSession() → AuthenticateAsync() if expired        │
│    2. EnumerateThreadsAsync(sectionUrl) via IForumScraper       │
│    3. ExtractPostContentAsync(threadUrl) via IForumScraper      │
│    4. ExtractLinksAsync(postContent) via IForumScraper           │
│    5. ILinkParser.Parse(url) for each extracted link            │
│    6. IMetadataSource.SearchByTitleAsync() for IMDB matching    │
│    7. QualityAnalysisService.Rank(postContent) for quality      │
│    8. Persist results to DB via repositories                     │
│    9. Update IRunStatusService + push via SignalR                │
│                                                                  │
│  Rate limiting: SemaphoreSlim(maxConcurrency) from Settings     │
│  Per-forum delay: Task.Delay(forum.PolitenessDelayMs)           │
│  HTTP resilience: Polly retry + circuit breaker (configurable)  │
└──────────────────────────────────────────────────────────────────┘
```

### Key Abstractions

| Interface | Location | Implementation | Purpose |
|-----------|----------|---------------|---------|
| `IScrapingJobQueue` | Application | `ScrapingJobQueue` (Infrastructure) | `Channel<ScrapeRequest>` wrapping. Enqueue from UI, dequeue in BackgroundService. |
| `IScrapingWorker` | Application | `ScrapingWorker` (Infrastructure) | All scraping orchestration logic. Unit-testable without hosting. |
| `IRunStatusService` | Application | `RunStatusService` (Infrastructure) | Singleton. BackgroundService writes; UI reads. Thread-safe concurrent dictionary of active runs. |

### Thin-Shell Pattern

The `BackgroundService` subclass in `SeriesScraper.Web.BackgroundServices` contains ONLY:
1. Constructor injection of `IScrapingJobQueue` and `IScrapingWorker`
2. `ExecuteAsync` loop: dequeue from channel → call `IScrapingWorker.ExecuteAsync`
3. No business logic, no direct DB access, no HTTP calls

This ensures:
- `IScrapingWorker` is fully unit-testable by mocking `IForumScraper`, `IMetadataSource`, repositories
- `BackgroundService` lifecycle concerns (startup, shutdown, cancellation) are isolated from business logic

### Real-Time Progress via SignalR

```
IScrapingWorker completes an item
        ↓
Updates IRunStatusService (in-memory singleton)
        ↓
Calls IHubContext<RunProgressHub>.Clients.Group(runId).SendAsync("ItemCompleted", ...)
        ↓
Blazor component receives update, re-renders
```

- No polling. No timer-based refresh.
- `RunProgressHub` is a standard SignalR hub in `SeriesScraper.Web.Hubs`
- Blazor components connect to the hub on page load, join a group by `runId`

### Cancellation Pattern

1. Each `ScrapeRequest` carries a `CancellationTokenSource`
2. Cancel button on UI calls `IScrapingJobQueue.CancelRun(runId)`
3. `CancelRun` triggers `CancellationTokenSource.Cancel()` on the active request
4. `IScrapingWorker` checks `CancellationToken.IsCancellationRequested` between items
5. On cancellation: current item completes (no mid-request abort), then run status → `Partial`

### Resume Pattern

1. On resume: load `ScrapeRunItems` with `status = Done` from DB
2. Build skip set of completed `post_url` values
3. Re-enqueue the same `ScrapeRequest` with skip set attached
4. `IScrapingWorker` skips items present in the skip set

### IMDB Import Pipeline (Separate BackgroundService)

```
┌───────────────────────────────────────────────────────────────┐
│ ImdbImportBackgroundService (thin shell in Web project)       │
│                                                               │
│  Delegates to: IImdbImportWorker.ExecuteAsync(ct)            │
│                                                               │
│  Runs on configurable schedule (from Settings table)         │
│  Pipeline:                                                    │
│    1. Download TSV datasets to temp files                    │
│    2. Validate (gzip header, minimum row count)              │
│    3. Bulk import to staging tables (NpgsqlBinaryImporter)   │
│    4. Upsert to live tables (INSERT ... ON CONFLICT)         │
│    5. Truncate staging tables                                │
│    6. Update DataSourceImportRuns status                      │
│                                                               │
│  Memory ceiling enforced per chunk (default 256 MB)          │
│  Malformed rows logged at WARN, import continues             │
└───────────────────────────────────────────────────────────────┘
```

### Forum Structure Refresh (Separate BackgroundService)

```
┌───────────────────────────────────────────────────────────────┐
│ ForumStructureBackgroundService (thin shell in Web project)   │
│                                                               │
│  Delegates to: IForumStructureWorker.ExecuteAsync(ct)        │
│                                                               │
│  Runs on configurable schedule (from Settings table)         │
│  For each active forum:                                       │
│    1. IForumScraper.EnumerateSectionsAsync(baseUrl, depth)   │
│    2. Detect language of section names (#6 research)          │
│    3. Classify content type (TV Series / Movie / Other)      │
│    4. Upsert ForumSections (add new, update existing, deactivate missing) │
└───────────────────────────────────────────────────────────────┘
```

## Alternatives Considered

### Alternative: External job queue (Hangfire, Quartz.NET, RabbitMQ)

Rejected because:
- Single-user app with at most 1 concurrent scrape run
- `Channel<T>` is in-process, zero infrastructure overhead
- No persistence needed for the queue itself — run state is persisted in DB
- External dependencies add complexity for no scaling benefit

### Alternative: Polling-based UI updates

Rejected because:
- Blazor Server already uses SignalR for all component rendering
- Polling adds unnecessary DB load and latency
- SignalR push is immediate and efficient

## Consequences

- Three independent `BackgroundService` shells: scraping, IMDB import, forum structure refresh
- All business logic in testable worker services, not in `BackgroundService` subclasses
- `Channel<T>` provides backpressure if needed (bounded channel option)
- SignalR hub enables real-time progress without polling
- Cancellation is cooperative — no mid-request aborts
