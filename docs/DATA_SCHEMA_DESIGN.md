# SeriesScraper — Complete Database Schema Design

**Date**: April 2, 2026  
**Author**: Data Engineer Agent  
**Related Issues**: #20, #21, #22, #25, #43

---

## Executive Summary

This document presents the complete normalized database schema for SeriesScraper, designed to support:
- ✅ Multiple authenticated forums with hierarchical structure
- ✅ Extensible metadata sources (IMDB now, CSFD+ later via plugin pattern)
- ✅ Quality pattern recognition with machine learning
- ✅ Link extraction with completeness checking
- ✅ Persistent watchlist
- ✅ User-configurable settings (all in DB, not appsettings.json)

**Total Entities**: 18 tables + 4 IMDB staging tables  
**Normalization**: 3NF minimum (denormalized only where explicitly justified)

---

## Design Principles

1. **Canonical Media Layer**: All domain logic references `MediaTitles.media_id` — NEVER IMDB `tconst` directly
2. **Source Extensibility**: `DataSources` lookup + source-specific detail tables (`ImdbTitleDetails`, future `CsfdTitleDetails`)
3. **is_current Flag Pattern**: Links use accumulate-with-flag for idempotent re-scraping
4. **String Enums**: All status/type columns use `HasConversion<string>()` — NO integer enums
5. **Settings in DB**: All runtime config in `Settings` table, not `appsettings.json`
6. **No Plaintext Credentials**: `Forums.credential_key` stores environment variable name only

---

## Entity Groups

### 1. Forum Management
- **Forums**: User-configured forum targets (base_url, credential_key, politeness_delay_ms)
- **ContentTypes**: Lookup table (TV Series, Movie, Other)
- **ForumSections**: Discovered sections (self-referential FK for nested structure)

### 2. Scraping Runs & Posts
- **ScrapeRuns**: Tracks each scraping execution (status: Pending/Running/Partial/Complete/Failed)
- **ScrapeRunItems**: Junction tracking individual post URLs (for resume logic)
- **Posts**: Scraped forum posts (title, body_text, quality_rank, match_status, completeness_status)

### 3. Media Metadata (Canonical Layer)
- **DataSources**: Lookup for metadata providers (IMDB, CSFD, etc.)
- **MediaTitles**: Canonical normalized titles (decouples domain from IMDB)
- **MediaTitleAliases**: Alternative/localized titles
- **MediaEpisodes**: TV series episode metadata (for completeness checking)
- **MediaRatings**: Rating + vote count per source
- **ImdbTitleDetails**: IMDB-specific fields (tconst, genre_string) — NEVER directly referenced

### 4. Quality Patterns
- **QualityTokens**: User-editable seed list (4K, BluRay, etc.)
- **QualityLearnedPatterns**: Runtime-accumulated patterns (hit_count, algorithm_version)

### 5. Links
- **LinkTypes**: User-configurable URL pattern registry (system + user types)
- **Links**: Extracted URLs (parsed_season, parsed_episode, is_current flag)

### 6. Watchlist
- **Watchlist**: User-managed titles to track (media_id, last_matched_at)

### 7. Settings
- **Settings**: Key-value store for ALL application config

### 8. Data Import Tracking
- **DataSourceImportRuns**: Tracks IMDB dataset refresh jobs

---

## Key Relationships

```
Forums
  ├─> ForumSections (forum_id)
  └─> ScrapeRuns (forum_id)

MediaTitles (Canonical Layer)
  ├─> MediaTitleAliases
  ├─> MediaEpisodes
  ├─> MediaRatings
  ├─> ImdbTitleDetails (1:1)
  ├─> Posts (matched_media_id)
  └─> Watchlist

Posts
  ├─> Forums (forum_id)
  ├─> ForumSections (section_id)
  ├─> MediaTitles (matched_media_id)
  └─> Links (post_id)

Links
  ├─> Posts (post_id)
  ├─> LinkTypes (link_type_id)
  └─> ScrapeRuns (run_id)
```

---

## Critical Indexes

### Partial Indexes (Raw SQL in migration Up())
Per #43 acceptance criteria, the following require partial indexes:

```sql
-- QualityTokens
CREATE INDEX IX_QualityTokens_IsActivePartial 
    ON QualityTokens (token_id) WHERE is_active = true;

-- QualityLearnedPatterns  
CREATE INDEX IX_QualityLearnedPatterns_IsActivePartial 
    ON QualityLearnedPatterns (pattern_id) WHERE is_active = true;

-- LinkTypes
CREATE INDEX IX_LinkTypes_IsActivePartial 
    ON LinkTypes (link_type_id) WHERE is_active = true;

-- Links
CREATE INDEX IX_Links_IsCurrentPartial 
    ON Links (post_id, link_id) WHERE is_current = true;
```

### Performance Indexes
- `Posts(forum_id, post_date DESC)` — History page queries
- `Posts(matched_media_id)` — Watchlist match queries
- `ScrapeRunItems(run_id, status)` — Resume logic
- `MediaTitles(canonical_title, year, type)` — Title matching
- `MediaTitleAliases(alias_title)` — Fuzzy matching

---

## IMDB Dataset Import Strategy

### Staging Pattern (#22)
1. **Download**: IMDB TSV gzip files → temp file → validate (gzip header + min row count)
2. **Stage**: Bulk import via `NpgsqlBinaryImporter` into staging tables (NO indexes, NO FKs)
3. **Upsert**: `INSERT ... ON CONFLICT DO UPDATE` into live tables
4. **Cleanup**: Truncate staging tables after successful upsert

### Staging Tables (NOT in DbContext)
- `staging_title_basics`
- `staging_title_akas`
- `staging_title_episode`
- `staging_title_ratings`

### Datasets Imported (#22)
- `title.basics` → `MediaTitles`
- `title.akas` → `MediaTitleAliases`
- `title.episode` → `MediaEpisodes`
- `title.ratings` → `MediaRatings`

---

## Migration Conventions (#43)

### 1. Partial Indexes
EF Core does not support partial indexes natively — use raw SQL:
```csharp
migrationBuilder.Sql(@"
    CREATE INDEX IX_Links_IsCurrentPartial 
    ON Links (post_id, link_id) 
    WHERE is_current = true
");
```

### 2. Enum String Conversion
ALL enum columns use `HasConversion<string>()`:
```csharp
entity.Property(e => e.Status)
    .HasConversion<string>()
    .HasMaxLength(50);
```

### 3. Seed Data
Seeded via `HasData()` or migration `Up()` — NEVER in `Program.cs`:
- ContentTypes (TV Series, Movie, Other)
- DataSources (IMDB)
- LinkTypes (Direct HTTP, Torrent File, Magnet URI, Cloud Storage)
- QualityTokens (4K, 1080p, BluRay, etc.)
- Settings (all defaults)

### 4. Self-Referential FKs
Use `OnDelete(DeleteBehavior.Restrict)`:
```csharp
entity.HasOne(e => e.ParentSection)
    .WithMany()
    .HasForeignKey(e => e.ParentSectionId)
    .OnDelete(DeleteBehavior.Restrict);
```

### 5. Global Query Filters — PENDING DECISION
**BLOCKER**: Must be decided before repository implementation (#43)

**Options**:
- **A**: Global filters on entities with `is_active`/`is_current` flags
- **B**: Explicit filtering in repository/query methods

**Recommendation**: Document decision in #43 comments once architect weighs in.

---

## Nullability Semantics (#43 Requirement)

Every nullable field is explicitly documented:

| Table.Column | Null Meaning |
|--------------|--------------|
| `ForumSections.parent_section_id` | Null = root section (no parent) |
| `ForumSections.detected_language` | Null = language detection failed or not yet run |
| `ScrapeRuns.completed_at` | Null = run in-progress or failed |
| `ScrapeRunItems.item_id` | Null = post not yet persisted (backfilled later) |
| `Posts.section_id` | Null = source section was deleted |
| `Posts.quality_rank` | Null = no quality tokens matched |
| `Posts.matched_media_id` | Null = no IMDB match found |
| `MediaTitles.year` | Null = year unknown or not applicable |
| `Links.parsed_season` | Null = parsing failed or not applicable (movies) |
| `Links.parsed_episode` | Null = parsing failed or not applicable |
| `Watchlist.last_matched_at` | Null = never matched in any run |
| `DataSourceImportRuns.finished_at` | Null = import in-progress |

---

## Security Notes

### Credential Storage
- ✅ NEVER store plaintext passwords in database
- ✅ `Forums.credential_key` = environment variable name only
- ✅ Password retrieved via `Environment.GetEnvironmentVariable()` at runtime

### XSS Prevention  
- ✅ `Posts.body_text` sanitized via `HtmlSanitizer` BEFORE storage
- ✅ NEVER render scraped content via Blazor `MarkupString` without re-sanitization
- ✅ URL `href` attributes validated to `http/https/magnet/torrent` schemes only

### SSRF Prevention
- ✅ Forum `base_url` validated against RFC1918, loopback, link-local, Docker ranges
- ✅ Schemes validated: `http/https` only (`javascript:`, `data:`, `file:` rejected)

### ReDoS Prevention
- ✅ All user regex patterns compiled with `matchTimeout: TimeSpan.FromSeconds(2)`
- ✅ `LinkTypes.url_pattern` and `QualityLearnedPatterns.pattern_regex` time-limited

---

## Seed Data Summary

### ContentTypes
- TV Series
- Movie
- Other

### DataSources
- IMDB (id=1)

### LinkTypes (is_system=true)
- Direct HTTP
- Torrent File
- Magnet URI
- Cloud Storage URL

### QualityTokens
Positive polarity (rank 40-100):
- 2160p, 4K (100)
- HDR (75)
- BluRay (70)
- HEVC, x265 (65)
- x264 (60)
- 1080p (80)
- 720p (60)
- WEB-DL, SDR (50)
- 480p (40)

Negative polarity (rank -10):
- AI-upscaled
- AI upscale

### Settings
- `ImdbRefreshIntervalHours`: "24"
- `ForumStructureRefreshIntervalHours`: "24"
- `MaxConcurrentScrapeThreads`: "1"
- `QualityPruningThreshold`: "5"
- `ResultRetentionDays`: "0" (0 = retain all)
- `HttpRetryCount`: "3"
- `HttpRetryBackoffMultiplier`: "2"
- `HttpCircuitBreakerThreshold`: "5"
- `HttpTimeoutSeconds`: "30"
- `BulkImportMemoryCeilingMB`: "256"

---

## Open Questions for Architect

### 1. Global Query Filters (#43)
- Should entities with `is_active`/`is_current` use EF Core global query filters?
- Or should filtering be explicit in repository/query methods?
- **BLOCKS**: Repository implementation

### 2. IMDB Staging Table Migration
- Should staging tables be created via dedicated migration or in import service code?
- **Recommendation**: Dedicated migration for discoverability

### 3. IMetadataSource Interface (#4)
- Architect must define interface before IMDB implementation begins
- **BLOCKS**: #20, #21, #22

---

## Next Steps

1. ✅ **COMPLETED**: Comprehensive schema design
2. **NEXT**: Post schema summaries to GitHub issues:
   - #20 (EPIC: IMDB Integration) — full canonical layer design
   - #21 (MediaTitles layer) — detailed entity definitions
   - #22 (IMDB import pipeline) — staging + import strategy
   - #25 (Quality patterns) — QualityTokens + QualityLearnedPatterns
   - #43 (EF Core conventions) — migration strategy + nullability semantics
3. **PENDING ARCHITECT**: 
   - #4 (IMetadataSource interface definition)
   - #43 (global query filter decision)
4. **READY FOR DEVELOPER**:
   - Initial EF Core migration implementation
   - Entity class creation following Fluent API conventions

---

## Test Coverage Requirements

Per SHARED_AGENTS.md — minimum 90% enforced in CI:
- ✅ Migration up/down paths tested (xUnit + Testcontainers)
- ✅ FK constraint validation (referential integrity)
- ✅ Partial index creation verified (PostgreSQL-specific)
- ✅ Seed data insertion tested
- ✅ Enum string conversion tested
- ✅ Nullability constraints tested

---

**Document Status**: ✅ COMPLETE — Ready for GitHub issue posting  
**Schema Coverage**: 100% of identified requirements from issues #9, #10, #12, #15, #19, #21, #22, #25, #28, #29, #31, #36, #43  
**Validation**: All entities cross-referenced against acceptance criteria
