# ADR-004: Data Model Architecture — Canonical Layer and Schema Conventions

**Status**: Accepted  
**Date**: 2026-04-02  
**Decision Makers**: Architect Agent  
**Related Issues**: #21, #22, #25, #28, #29, #43

---

## Context

SeriesScraper stores data from multiple external sources (IMDB now, CSFD later) and must maintain a canonical internal representation. The schema must support:

1. Source-independent media title references (no tconst in foreign keys)
2. Pluggable metadata sources with source-specific detail tables
3. Link management with accumulate-with-flag re-scrape pattern
4. Quality pattern recognition with extensible pattern registry
5. Forum structure with hierarchical sections

## Decision: Canonical Media Layer

### Entity Relationship Overview

```
DataSources (lookup)
    │
    ├──→ MediaTitles (canonical)
    │       ├──→ MediaTitleAliases
    │       ├──→ MediaEpisodes
    │       ├──→ MediaRatings
    │       ├──→ ImdbTitleDetails (source-specific)
    │       └──→ WatchlistItems
    │
Forums
    ├──→ ForumSections
    │       └──→ ContentTypes (lookup)
    └──→ ScrapeRuns
            └──→ ScrapeRunItems
                    └──→ ScrapedPosts
                            ├──→ Links
                            │       └──→ LinkTypes (registry)
                            ├──→ PostQualityScores
                            │       └──→ QualityPatterns (registry)
                            └──→ MediaTitles (FK: match)

Settings (key-value)
DataSourceImportRuns (import tracking)
```

### Schema Conventions (for Data Engineer — #43)

These conventions apply to ALL tables in the system:

1. **Table names**: PascalCase plural (e.g., `MediaTitles`, `ForumSections`)
2. **Column names**: snake_case (e.g., `media_id`, `canonical_title`, `created_at`)
3. **Primary keys**: `{entity_singular}_id` (e.g., `media_id`, `forum_id`, `link_id`)
4. **Foreign keys**: Same name as the referenced PK column
5. **Status columns**: `HasConversion<string>()` — string storage, not integer enums
6. **Timestamps**: `created_at`, `updated_at`, `last_modified_at` — UTC `timestamp with time zone`
7. **Soft deletes**: `is_active` boolean column where applicable (ForumSections), NOT soft delete on all tables
8. **EF Core**: Fluent API only (no data annotations). Entity configurations in dedicated `IEntityTypeConfiguration<T>` classes.
9. **Migrations**: Versioned, idempotent. Seed data via `HasData()` or `Up()`.
10. **Indexes**: Explicit indexes for FK columns and frequently queried columns. Partial indexes where specified (e.g., `is_current = true` on Links).

### Key Entity Definitions

**Forums**
```
Forums: forum_id (PK), name, base_url, username, credential_key,
        crawl_depth (default 1), politeness_delay_ms (default 500),
        is_active, created_at, updated_at
```

**ForumSections**
```
ForumSections: section_id (PK), forum_id (FK), parent_section_id (nullable self-FK),
               url (unique), name, detected_language, content_type_id (FK),
               last_crawled_at, is_active
```

**ContentTypes (lookup)**
```
ContentTypes: content_type_id (PK), name (unique)
Seed: TV Series, Movie, Other
```

**ScrapeRuns**
```
ScrapeRuns: run_id (PK), forum_id (FK), status (string: Pending/Running/Partial/Complete/Failed),
            started_at, completed_at (nullable), total_items, processed_items
```

**ScrapeRunItems**
```
ScrapeRunItems: run_item_id (PK), run_id (FK), post_url (not null),
                item_id (nullable FK), status (string: Pending/Processing/Done/Failed/Skipped),
                processed_at
```

**MediaTitles (canonical)**
```
MediaTitles: media_id (PK), canonical_title (not null), year (nullable),
             type (string: movie/series/episode), source_id (FK to DataSources),
             created_at, updated_at
```

**MediaTitleAliases**
```
MediaTitleAliases: alias_id (PK), media_id (FK), alias_title (not null),
                   language (nullable), region (nullable)
Index: (alias_title) for lookups, (media_id) for FK
```

**MediaEpisodes**
```
MediaEpisodes: episode_id (PK), media_id (FK), season (not null),
               episode_number (not null)
Unique: (media_id, season, episode_number)
```

**MediaRatings**
```
MediaRatings: media_id (FK to MediaTitles, PK), rating (decimal),
              vote_count (int), source_id (FK to DataSources)
```

**ImdbTitleDetails (source-specific)**
```
ImdbTitleDetails: media_id (FK to MediaTitles, PK), tconst (string, unique),
                  genre_string (nullable)
Note: tconst is NEVER referenced from Watchlist, ScrapeRun, or Link entities
```

**DataSources (lookup)**
```
DataSources: source_id (PK), name (unique), description
Seed: IMDB (id=1)
```

**Links**
```
Links: link_id (PK), post_id (FK), url (not null), link_type_id (FK to LinkTypes),
       parsed_season (nullable), parsed_episode (nullable), created_at,
       is_current (bool, default true), run_id (FK to ScrapeRuns)
Partial index: (post_id) WHERE is_current = true
```

**LinkTypes (registry)**
```
LinkTypes: link_type_id (PK), name (unique), url_pattern (regex),
           is_system (bool), icon_class (nullable), is_active (bool, default true)
Seed: Direct HTTP, Torrent File, Magnet URI, Cloud Storage URL (all is_system=true)
```

**QualityPatterns (registry)**
```
QualityPatterns: pattern_id (PK), regex_pattern (not null), quality_label (not null),
                 rank (int), hit_count (int, default 0), is_active (bool, default true)
```

**WatchlistItems**
```
WatchlistItems: watchlist_item_id (PK), media_id (nullable FK to MediaTitles),
                user_title (not null), added_at, last_matched_at (nullable)
```

**Settings (key-value)**
```
Settings: key (PK, string), value (string), description (string),
          last_modified_at (datetime)
```

**DataSourceImportRuns**
```
DataSourceImportRuns: import_run_id (PK), source_id (FK to DataSources),
                      started_at, finished_at (nullable),
                      status (string: Running/Complete/Failed/Partial),
                      rows_imported (int), error_message (nullable)
```

## Consequences

- All application code references `media_id`, never `tconst` — adding CSFD requires only a new detail table + IMetadataSource implementation
- Canonical layer adds one level of indirection but eliminates source coupling
- Schema conventions are enforced by code review and can be validated by convention tests
- Staging table pattern for IMDB import ensures live tables remain queryable during bulk imports
