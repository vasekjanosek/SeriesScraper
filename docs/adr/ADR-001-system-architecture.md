# ADR-001: System Architecture — Clean Architecture with Blazor Server

**Status**: Accepted  
**Date**: 2026-04-02  
**Decision Makers**: Architect Agent  
**Related Issues**: #1, #2, #3, #4, #14, #37

---

## Context

SeriesScraper is a private single-user web application that scrapes authenticated forums for download links, cross-checks them against IMDB datasets, evaluates relevance, and presents results in a Blazor UI. The system requires:

1. **Pluggable interfaces** — `IForumScraper` (forum software varies), `IMetadataSource` (IMDB now, CSFD later), `ILinkParser` (open/closed link type classification)
2. **Background services** — Scrape runs, IMDB import pipeline, forum structure refresh — all `BackgroundService`/`IHostedService`
3. **Real-time UI** — Run progress page (#32) requires server push via SignalR
4. **Testability** — 90% coverage requirement means domain and application logic must be testable without infrastructure dependencies
5. **Single deployment target** — Docker Compose with PostgreSQL, no external API consumers planned

## Decision: Clean Architecture (4-Project Solution)

### Solution Structure

```
SeriesScraper.sln
├── src/
│   ├── SeriesScraper.Domain/              ← Entities, interfaces (ports), value objects, enums
│   ├── SeriesScraper.Application/         ← Application services, DTOs, use cases, abstractions
│   ├── SeriesScraper.Infrastructure/      ← EF Core DbContext, repositories, HTTP clients, IMDB pipeline
│   └── SeriesScraper.Web/                 ← Blazor Server host, pages, components, DI registration
├── tests/
│   ├── SeriesScraper.Domain.Tests/
│   ├── SeriesScraper.Application.Tests/
│   ├── SeriesScraper.Infrastructure.Tests/  ← Integration tests (Testcontainers)
│   └── SeriesScraper.Web.Tests/             ← Component tests (bUnit)
└── docs/
    └── adr/                                 ← Architecture Decision Records
```

### Component Responsibilities

| Project | Responsibility | Dependencies |
|---------|---------------|--------------|
| `SeriesScraper.Domain` | Domain entities (`Forum`, `ScrapeRun`, `MediaTitle`, `Link`, etc.), interface contracts (`IForumScraper`, `IMetadataSource`, `ILinkParser`), value objects, domain exceptions | None (zero external dependencies) |
| `SeriesScraper.Application` | Application services (`ScrapingOrchestrator`, `TitleMatchingService`, `QualityAnalysisService`), DTOs, `IScrapingJobQueue`, `IScrapingWorker`, `IRunStatusService`, repository interfaces | Domain only |
| `SeriesScraper.Infrastructure` | EF Core `AppDbContext`, repository implementations, `HttpClient` factories, concrete `IForumScraper` implementation, IMDB TSV import pipeline, Serilog configuration, `DataProtection` credential encryption | Domain, Application, EF Core, Npgsql, Serilog, Polly |
| `SeriesScraper.Web` | Blazor Server host, Razor pages/components, DI composition root, `BackgroundService` shells, SignalR hub for run progress, MudBlazor UI | All projects |

### Dependency Rule

```
Web → Infrastructure → Application → Domain
        ↕ (implements)     ↕ (defines interfaces)
```

- **Domain** has ZERO NuGet dependencies. Only C# primitives and interfaces.
- **Application** references Domain only — defines use cases and repository interfaces.
- **Infrastructure** implements Domain/Application interfaces — references EF Core, Npgsql, Polly, Serilog.
- **Web** is the composition root — registers all DI, hosts BackgroundServices, contains Blazor components.

## Alternatives Evaluated

### Alternative 1: Traditional N-Tier (3-Layer)

```
Web → Business Logic → Data Access
```

**Rejected because:**
- Data access and domain logic are co-located, making unit testing harder
- Pluggable interfaces (IForumScraper, IMetadataSource) would reference infrastructure concerns
- No clear place for interface contracts without creating circular dependencies
- Extensibility points (adding CSFD, new forum scrapers) would require modifying existing layers

### Alternative 2: Vertical Slices (Feature Folders)

Each feature (Forum Management, Scraping, IMDB, etc.) is a self-contained module with its own data access, logic, and presentation.

**Rejected because:**
- Shared entities (MediaTitle, Link, ScrapeRun) are referenced across multiple features
- Pluggable interfaces span multiple features (IForumScraper is used by Forum Management, Structure Learning, and Scraping Engine)
- Background services need to coordinate across features (scraping → matching → link extraction in one pipeline)
- Added namespace complexity for a team of agents working in parallel

## Blazor Hosting Model: Blazor Server

**Chosen: Blazor Server** over Blazor WebAssembly.

| Factor | Blazor Server | Blazor WebAssembly |
|--------|--------------|-------------------|
| SignalR for real-time (#32) | Built-in, zero config | Must add separately |
| API layer | Not needed — direct DI | Requires separate REST/gRPC API project |
| Deployment complexity | Single Docker container | Two containers (API + static files) |
| Connection scaling | Non-issue (single user, private) | N/A |
| Client requirements | Thin — any browser | Heavier WASM download |

**Rejected: Blazor WebAssembly** — Would require a separate API project with no consumers other than the single UI, adding complexity for zero benefit.

## API Design: No REST API — Direct Service Injection

Blazor components and `BackgroundService` shells both inject Application services via DI. No HTTP API layer.

**Rejected: Minimal API endpoints** — Adds unnecessary HTTP serialization/deserialization overhead for a single-user app with no external API consumers. Can be added later as a thin layer.

## Namespace Convention

```
SeriesScraper.Domain.Entities          ← Forum, ScrapeRun, MediaTitle, Link, etc.
SeriesScraper.Domain.Interfaces        ← IForumScraper, IMetadataSource, ILinkParser
SeriesScraper.Domain.ValueObjects      ← ContentType, LinkScheme, MatchStatus, etc.
SeriesScraper.Domain.Exceptions        ← DomainException, ScrapingException, etc.

SeriesScraper.Application.Services     ← ScrapingOrchestrator, TitleMatchingService, etc.
SeriesScraper.Application.DTOs         ← ScrapeRunDto, ForumDto, ResultDto, etc.
SeriesScraper.Application.Interfaces   ← IForumRepository, IScrapeRunRepository, IScrapingJobQueue, etc.

SeriesScraper.Infrastructure.Data      ← AppDbContext, entity configurations, migrations
SeriesScraper.Infrastructure.Repositories ← ForumRepository, ScrapeRunRepository, etc.
SeriesScraper.Infrastructure.Scraping  ← Concrete IForumScraper implementations
SeriesScraper.Infrastructure.Imdb      ← IMDB TSV import pipeline, ImdbMetadataSource
SeriesScraper.Infrastructure.Logging   ← Serilog configuration, destructuring policies

SeriesScraper.Web.Pages                ← Blazor pages
SeriesScraper.Web.Components           ← Shared Blazor components
SeriesScraper.Web.BackgroundServices   ← Thin BackgroundService shells
SeriesScraper.Web.Hubs                 ← SignalR hub for real-time run progress
```

## Technology Selections

| Concern | Technology | Justification |
|---------|-----------|---------------|
| ORM | EF Core 8 | Project requirement. Fluent API only (no data annotations). |
| DB | PostgreSQL 16 | Project requirement. Npgsql provider. |
| Bulk import | Npgsql `COPY` (`NpgsqlBinaryImporter`) | Required by #22 for IMDB dataset import performance. |
| HTTP resilience | Polly v8 | Retry + circuit breaker for forum HTTP requests (#15). |
| Job queue | `System.Threading.Channels.Channel<T>` | Required by #15. In-process, no external broker needed. |
| UI framework | MudBlazor | Rich component library for Blazor. Cards, tables, chips, toggles per issue ACs. |
| Logging | Serilog | Required by #42. Structured logging with destructuring policies. |
| Testing | xUnit + FluentAssertions + Testcontainers + bUnit | Project requirement. |
| Static analysis | SonarAnalyzer.CSharp | Project requirement. |
| Containerization | Docker Compose | Project requirement. |

## Extensibility Points

1. **`IForumScraper`** (Domain) — Strategy pattern. One implementation per forum software. Registered in DI. Selected at runtime based on forum configuration.
2. **`IMetadataSource`** (Domain) — Strategy pattern. IMDB now; CSFD later. Keyed services in DI. `DataSources` lookup table maps to implementations.
3. **`ILinkParser`** (Domain) — Strategy pattern. One parser per link type. All registered via DI. New parsers added by implementing interface + inserting DB row.
4. **`IScrapingJobQueue`** (Application) — `Channel<T>` backed. Decouples HTTP layer from background processing.
5. **`IScrapingWorker`** (Application) — Thin-shell pattern. BackgroundService delegates to testable worker.
6. **`IRunStatusService`** (Application) — Singleton shared between BackgroundService and Blazor components.

## Security Implications

- **No app authentication for v1** — Private network only. Needs human review (#49).
- **Forum credentials** — Env-var key mapping (#10). Plaintext never in DB. DataProtection API for any at-rest encryption.
- **SSRF** — Forum URL validation at model layer. Deny RFC1918/loopback/link-local/Docker-internal (#46).
- **XSS** — Blazor `@` interpolation for scraped content. `MarkupString` prohibited for scraped data (#45).
- **CSRF** — Blazor Server SignalR has built-in anti-forgery (#47).
- **ReDoS** — All user-supplied regex compiled with `matchTimeout: 2s` (#48).

## Consequences

- Clear separation enables parallel development on Domain, Application, Infrastructure, and Web layers
- Zero-dependency Domain project ensures domain logic is fully unit-testable
- Blazor Server eliminates the need for a separate API project
- Clean Architecture adds ~4 projects of overhead vs N-Tier, but extensibility requirements justify it
- Strategy pattern at all integration points enables future plugins without modifying existing code
