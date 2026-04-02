# GitHub Issue Comments — Schema Design Summaries

This document contains the schema design comments that should be posted to each relevant GitHub issue.

---

## Issue #43 — EF Core migrations baseline and schema conventions

### Comment to Post:

## EF Core Migrations Baseline & Schema Conventions — Design Decisions

Per the acceptance criteria in #43, here are the foundational schema conventions that all developers must follow:

### 1. Partial Indexes (AC#1)
All tables with `is_current` or `is_active` flags require PostgreSQL partial indexes created via raw SQL in migration `Up()`:

```csharp
migrationBuilder.Sql(@"
    CREATE INDEX IX_QualityTokens_IsActivePartial 
    ON QualityTokens (token_id) WHERE is_active = true
");

migrationBuilder.Sql(@"
    CREATE INDEX IX_QualityLearnedPatterns_IsActivePartial 
    ON QualityLearnedPatterns (pattern_id) WHERE is_active = true
");

migrationBuilder.Sql(@"
    CREATE INDEX IX_LinkTypes_IsActivePartial 
    ON LinkTypes (link_type_id) WHERE is_active = true
");

migrationBuilder.Sql(@"
    CREATE INDEX IX_Links_IsCurrentPartial 
    ON Links (post_id, link_id) WHERE is_current = true
");
```

### 2. Global Query Filters Decision (AC#2) — PENDING ARCHITECT INPUT

**BLOCKER**: This decision must be finalized before repository implementation begins.

**Option A: Global Query Filters**
```csharp
modelBuilder.Entity<Link>()
    .HasQueryFilter(l => l.IsCurrent);

modelBuilder.Entity<QualityToken>()
    .HasQueryFilter(q => q.IsActive);
```
**Pros**: Automatic filtering, less boilerplate  
**Cons**: Harder to query historical data, must explicitly ignore filters

**Option B: Explicit Repository Filtering**
```csharp
public async Task<IEnumerable<Link>> GetCurrentLinksAsync(int postId)
{
    return await _context.Links
        .Where(l => l.PostId == postId && l.IsCurrent)
        .ToListAsync();
}
```
**Pros**: Explicit intent, easier to query all rows  
**Cons**: More boilerplate, risk of forgetting filter

**Recommendation**: Option B (explicit filtering) for these reasons:
- Resume logic and historical queries need all rows (including `is_current=false`)
- Query intent is clearer in repository methods
- Avoiding global filters prevents hidden bugs when .IgnoreQueryFilters() is forgotten

**Architect: Please confirm or override this recommendation.**

### 3. Enum String Conversion (AC#3, AC#7)
ALL status and type enum columns use `HasConversion<string>()`:

```csharp
entity.Property(e => e.Status)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();

entity.Property(e => e.Type)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();
```

Affected columns:
- `ScrapeRuns.status` (Pending/Running/Partial/Complete/Failed)
- `ScrapeRunItems.status` (Pending/Processing/Done/Failed/Skipped)
- `Posts.match_status` (Matched/Unmatched/Partial)
- `Posts.completeness_status` (Complete/Incomplete/Unknown)
- `MediaTitles.type` (movie/series/episode)
- `QualityTokens.polarity` (positive/negative)
- `QualityLearnedPatterns.polarity` (positive/negative)
- `QualityLearnedPatterns.source` (Seed/Learned/User)
- `DataSourceImportRuns.status` (Running/Complete/Failed/Partial)

### 4. Self-Referential FK (AC#4)
`ForumSections.parent_section_id` configured as:

```csharp
entity.HasOne(e => e.ParentSection)
    .WithMany()
    .HasForeignKey(e => e.ParentSectionId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.Restrict);
```

### 5. Staging Tables (AC#5)
IMDB import staging tables are created via raw SQL in a dedicated migration:

```sql
CREATE TABLE staging_title_basics (
    tconst VARCHAR(20) NOT NULL,
    titleType VARCHAR(50),
    primaryTitle VARCHAR(500),
    originalTitle VARCHAR(500),
    isAdult BOOLEAN,
    startYear INT,
    endYear INT,
    runtimeMinutes INT,
    genres VARCHAR(200)
);
-- No indexes, no FKs, no constraints — optimized for bulk COPY
```

These tables are NOT modeled as EF Core entities and do NOT appear in `DbContext.OnModelCreating`.

### 6. Seed Data (AC#6)
ALL seed data is inserted via migration `HasData()` or `Up()` — NEVER in `Program.cs`:

- `ContentTypes` (TV Series, Movie, Other)
- `DataSources` (IMDB)
- `LinkTypes` (Direct HTTP, Torrent File, Magnet URI, Cloud Storage — all with `is_system=true`)
- `QualityTokens` (4K, 1080p, BluRay, etc. — positive polarity; AI-upscaled — negative polarity)
- `Settings` (all default values: ImdbRefreshIntervalHours=24, etc.)

### 7. Nullability Semantics — Explicit Documentation

Per AC (not listed but implied by precision standards), every nullable field is documented:

| Table.Column | Null Meaning |
|--------------|--------------|
| `ForumSections.parent_section_id` | Null = root section (no parent) |
| `ForumSections.detected_language` | Null = language detection failed or not yet run |
| `ScrapeRuns.completed_at` | Null = run in-progress or failed |
| `ScrapeRunItems.item_id` | Null = post not yet persisted (backfilled later) |
| `ScrapeRunItems.processed_at` | Null = item pending or in-progress |
| `Posts.section_id` | Null = source section was deleted |
| `Posts.quality_rank` | Null = no quality tokens matched |
| `Posts.matched_media_id` | Null = no IMDB match found |
| `MediaTitles.year` | Null = year unknown or not applicable |
| `MediaTitleAliases.language` | Null = language not specified in IMDB data |
| `MediaTitleAliases.region` | Null = region not specified in IMDB data |
| `Links.parsed_season` | Null = parsing failed or not applicable (movies) |
| `Links.parsed_episode` | Null = parsing failed or not applicable |
| `Watchlist.last_matched_at` | Null = never matched in any run |
| `DataSourceImportRuns.finished_at` | Null = import in-progress |
| `DataSourceImportRuns.error_message` | Null = no error (success or in-progress) |

### Output Deliverables

✅ This decision record (covers AC Output bullet 1)  
🔄 Initial EF Core migration(s) — **NEXT STEP** (Developer agent)  
🔄 `CONTRIBUTING.md` developer guide update — **NEXT STEP** (Developer agent)

---

## Issue #21 — Canonical media title layer (MediaTitles)

### Comment to Post:

## MediaTitles Canonical Layer — Schema Design

Per #21 acceptance criteria, here is the complete schema for the canonical media layer that decouples all application logic from IMDB-specific identifiers.

### Entity Definitions

#### 1. DataSources (AC#7)
External metadata provider lookup table.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| source_id | int | PK, IDENTITY | Unique source identifier |
| name | string | NOT NULL, UNIQUE | Source name |

**Seed Data** (via migration `HasData()`):
```csharp
modelBuilder.Entity<DataSource>().HasData(
    new DataSource { SourceId = 1, Name = "IMDB" }
);
```

#### 2. MediaTitles (AC#1)
Canonical normalized media titles from all sources.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| media_id | int | PK, IDENTITY | Unique media identifier |
| canonical_title | string | NOT NULL | Normalized canonical title |
| year | int | NULL | Release year |
| type | string | NOT NULL | Enum: movie, series, episode (HasConversion<string>()) |
| source_id | int | NOT NULL, FK → DataSources | Primary source |

**EF Core Configuration** (AC#9):
```csharp
entity.HasKey(e => e.MediaId);

entity.Property(e => e.Type)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();

entity.HasOne(e => e.DataSource)
    .WithMany()
    .HasForeignKey(e => e.SourceId)
    .OnDelete(DeleteBehavior.Restrict);

entity.HasIndex(e => new { e.CanonicalTitle, e.Year, e.Type })
    .HasDatabaseName("IX_MediaTitles_TitleMatching");
```

#### 3. MediaTitleAliases (AC#2)
Alternative/localized titles for media.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| alias_id | int | PK, IDENTITY | Unique alias identifier |
| media_id | int | NOT NULL, FK → MediaTitles | Parent media |
| alias_title | string | NOT NULL | Alternative title |
| language | string | NULL | ISO 639-1 language code |
| region | string | NULL | ISO 3166-1 alpha-2 region code |

**EF Core Configuration**:
```csharp
entity.HasOne(e => e.MediaTitle)
    .WithMany(m => m.Aliases)
    .HasForeignKey(e => e.MediaId)
    .OnDelete(DeleteBehavior.Cascade);

entity.HasIndex(e => e.AliasTitle)
    .HasDatabaseName("IX_MediaTitleAliases_FuzzyMatching");
```

**Notes**:
- Populated from IMDB `title.akas` dataset
- `language` and `region` are nullable (IMDB sometimes omits them)

#### 4. MediaEpisodes (AC#3)
Episode metadata for TV series.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| episode_id | int | PK, IDENTITY | Unique episode identifier |
| media_id | int | NOT NULL, FK → MediaTitles | Parent series |
| season | int | NOT NULL | Season number |
| episode_number | int | NOT NULL | Episode number within season |

**EF Core Configuration**:
```csharp
entity.HasOne(e => e.MediaTitle)
    .WithMany(m => m.Episodes)
    .HasForeignKey(e => e.MediaId)
    .OnDelete(DeleteBehavior.Cascade);

entity.HasIndex(e => new { e.MediaId, e.Season, e.EpisodeNumber })
    .IsUnique()
    .HasDatabaseName("IX_MediaEpisodes_Unique");
```

**Notes**:
- Populated from IMDB `title.episode` dataset
- Used for completeness checking (scraped links vs. expected episode count)

#### 5. MediaRatings (AC#4)
Rating metadata per source.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| media_id | int | NOT NULL, FK → MediaTitles | Media being rated |
| rating | decimal(3,1) | NOT NULL | Rating value (e.g., 8.5) |
| vote_count | int | NOT NULL | Number of votes |
| source_id | int | NOT NULL, FK → DataSources | Rating source |

**EF Core Configuration**:
```csharp
entity.HasKey(e => new { e.MediaId, e.SourceId });

entity.HasOne(e => e.MediaTitle)
    .WithMany(m => m.Ratings)
    .HasForeignKey(e => e.MediaId)
    .OnDelete(DeleteBehavior.Cascade);

entity.HasOne(e => e.DataSource)
    .WithMany()
    .HasForeignKey(e => e.SourceId)
    .OnDelete(DeleteBehavior.Restrict);
```

**Notes**:
- Composite PK allows multiple sources to rate the same media
- Populated from IMDB `title.ratings` dataset

#### 6. ImdbTitleDetails (AC#5)
IMDB-specific metadata (decoupled from canonical layer).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| media_id | int | NOT NULL, FK → MediaTitles | Parent media (1:1) |
| tconst | string | NOT NULL, UNIQUE | IMDB title identifier (e.g., tt0133093) |
| genre_string | string | NULL | Comma-separated genre list |
| runtime_minutes | int | NULL | Runtime in minutes |

**EF Core Configuration**:
```csharp
entity.HasKey(e => e.MediaId);

entity.HasIndex(e => e.Tconst)
    .IsUnique()
    .HasDatabaseName("IX_ImdbTitleDetails_Tconst");

entity.HasOne(e => e.MediaTitle)
    .WithOne(m => m.ImdbDetails)
    .HasForeignKey<ImdbTitleDetails>(e => e.MediaId)
    .OnDelete(DeleteBehavior.Cascade);
```

**CRITICAL RULE (AC#5, AC#6)**:
- `tconst` is NEVER referenced directly from `Watchlist`, `ScrapeRuns`, `Links`, or `Posts` entities
- ALL references use `MediaTitles.media_id`
- FK constraints enforce this at the database level

### Foreign Key Enforcement (AC#6)

Application entities reference `MediaTitles.media_id`:
```csharp
// Posts entity
entity.HasOne(e => e.MatchedMedia)
    .WithMany()
    .HasForeignKey(e => e.MatchedMediaId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);

// Watchlist entity
entity.HasOne(e => e.MediaTitle)
    .WithMany()
    .HasForeignKey(e => e.MediaId)
    .IsRequired()
    .OnDelete(DeleteBehavior.Cascade);
```

### Migration Structure (AC#8)

The initial migration for MediaTitles layer includes:
1. `DataSources` table + seed data
2. `MediaTitles` table
3. `MediaTitleAliases` table
4. `MediaEpisodes` table
5. `MediaRatings` table
6. `ImdbTitleDetails` table
7. All FK relationships
8. All indexes (including composite indexes for title matching)

Migration follows conventions from #43.

---

## Issue #22 — IMDB dataset import pipeline

### Comment to Post:

## IMDB Dataset Import Pipeline — Data Schema Design

Per #22 acceptance criteria, here is the schema design for the IMDB dataset import pipeline and staging tables.

### Staging Table Pattern (AC#3)

The import follows a three-phase pattern:
1. **Bulk import** → staging tables (no indexes, no FKs, optimized for COPY protocol)
2. **Upsert** → live tables via `INSERT ... ON CONFLICT DO UPDATE`
3. **Cleanup** → truncate staging tables after successful upsert

### Staging Table Definitions (AC#5)

These tables are created via raw SQL in a dedicated migration and are NOT modeled as EF Core entities.

#### staging_title_basics
```sql
CREATE TABLE staging_title_basics (
    tconst VARCHAR(20) NOT NULL,
    titleType VARCHAR(50),
    primaryTitle VARCHAR(500),
    originalTitle VARCHAR(500),
    isAdult BOOLEAN,
    startYear INT,
    endYear INT,
    runtimeMinutes INT,
    genres VARCHAR(200)
);
```

#### staging_title_akas
```sql
CREATE TABLE staging_title_akas (
    titleId VARCHAR(20) NOT NULL,
    ordering INT,
    title VARCHAR(500),
    region VARCHAR(10),
    language VARCHAR(10),
    types VARCHAR(100),
    attributes VARCHAR(500),
    isOriginalTitle BOOLEAN
);
```

#### staging_title_episode
```sql
CREATE TABLE staging_title_episode (
    tconst VARCHAR(20) NOT NULL,
    parentTconst VARCHAR(20),
    seasonNumber INT,
    episodeNumber INT
);
```

#### staging_title_ratings
```sql
CREATE TABLE staging_title_ratings (
    tconst VARCHAR(20) NOT NULL,
    averageRating DECIMAL(3,1),
    numVotes INT
);
```

**Note**: No indexes, no FK constraints, no defaults — pure staging for bulk ingestion.

### Import Progress Tracking (AC#6)

#### DataSourceImportRuns
Tracks each import job execution.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| import_run_id | int | PK, IDENTITY | Unique import run identifier |
| source_id | int | NOT NULL, FK → DataSources | Data source being imported (1=IMDB) |
| started_at | datetime | NOT NULL | Import start timestamp |
| finished_at | datetime | NULL | Import completion timestamp (null if in-progress) |
| status | string | NOT NULL | Enum: Running, Complete, Failed, Partial (HasConversion<string>()) |
| rows_imported | int | NOT NULL, DEFAULT 0 | Total rows successfully imported |
| error_message | string | NULL | Error details (if status=Failed) |

**EF Core Configuration**:
```csharp
entity.Property(e => e.Status)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();

entity.HasOne(e => e.DataSource)
    .WithMany()
    .HasForeignKey(e => e.SourceId)
    .OnDelete(DeleteBehavior.Restrict);

entity.HasIndex(e => new { e.SourceId, e.StartedAt })
    .HasDatabaseName("IX_DataSourceImportRuns_SourceHistory");
```

**Resume Logic**:
- On startup: check for `status='Running'` rows → transition to `'Partial'`
- Resume from last `rows_imported` count (if TSV supports resume)

### Import Flow Design (AC#1, AC#2, AC#4, AC#7, AC#9)

```csharp
// Pseudocode — implementation will be in #22 Developer task
public async Task<ImportResult> ImportImdbDatasetAsync()
{
    // 1. Download & Validate (AC#7)
    var tempFile = await DownloadDatasetAsync("title.basics.tsv.gz");
    ValidateGzipHeader(tempFile);
    ValidateMinimumRowCount(tempFile, minRows: 100000);

    // 2. Begin tracking
    var importRun = new DataSourceImportRun
    {
        SourceId = 1, // IMDB
        StartedAt = DateTime.UtcNow,
        Status = ImportStatus.Running
    };
    await _context.DataSourceImportRuns.AddAsync(importRun);
    await _context.SaveChangesAsync();

    try
    {
        // 3. Bulk import to staging (AC#1: Npgsql COPY protocol)
        using var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var writer = connection.BeginBinaryImport(
            "COPY staging_title_basics FROM STDIN (FORMAT BINARY)"
        );
        
        await foreach (var chunk in StreamTsvInChunksAsync(tempFile, chunkSizeMB: 256))
        {
            foreach (var row in chunk)
            {
                // Malformed row handling (AC#9)
                if (!TryParseRow(row, out var record))
                {
                    _logger.LogWarning("Malformed TSV row at line {LineNumber}: {RowContent}",
                        row.LineNumber, row.Content.Substring(0, 200));
                    continue; // Skip bad rows, continue import
                }
                
                await writer.WriteRowAsync(record.Tconst, record.TitleType, ...);
                importRun.RowsImported++;
            }
        }
        
        await writer.CompleteAsync();

        // 4. Defer FK constraints during upsert (AC#4)
        await _context.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED");

        // 5. Upsert into live tables (AC#3)
        await _context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO MediaTitles (canonical_title, year, type, source_id)
            SELECT 
                primaryTitle,
                startYear,
                CASE titleType
                    WHEN 'movie' THEN 'movie'
                    WHEN 'tvSeries' THEN 'series'
                    WHEN 'tvEpisode' THEN 'episode'
                    ELSE 'movie'
                END,
                1 -- IMDB source_id
            FROM staging_title_basics
            ON CONFLICT (tconst) DO UPDATE
            SET canonical_title = EXCLUDED.canonical_title,
                year = EXCLUDED.year
        ");

        // 6. Re-enable FK constraints (AC#4)
        await _context.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE");

        // 7. Truncate staging (AC#3)
        await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE staging_title_basics");

        // 8. Mark complete
        importRun.FinishedAt = DateTime.UtcNow;
        importRun.Status = ImportStatus.Complete;
        await _context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        importRun.Status = ImportStatus.Failed;
        importRun.ErrorMessage = ex.ToString();
        await _context.SaveChangesAsync();
        throw;
    }
}
```

### Configuration Settings (AC#5, AC#11)

Read from `Settings` table:
- `ImdbRefreshIntervalHours`: Default 24
- `BulkImportMemoryCeilingMB`: Default 256
- `HttpTimeoutSeconds`: Default 30

Scheduler integration defined by #7 (background job approach).

### IMDB License Compliance (AC#8)

Comment block in import service class:
```csharp
/// <summary>
/// IMDB Dataset Import Service
/// 
/// LICENSING NOTICE:
/// This service imports publicly available IMDB datasets for NON-COMMERCIAL USE ONLY.
/// All IMDB data is subject to the IMDB Non-Commercial Licensing and copyright restrictions.
/// See: https://www.imdb.com/interfaces/
/// 
/// NO public-facing API endpoints may expose raw IMDB data.
/// IMDB data is used internally only for metadata matching and enrichment.
/// </summary>
```

### Background Service Pattern (AC#10)

```csharp
public class ImdbImportBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalHours = await _settingsService.GetIntAsync("ImdbRefreshIntervalHours");
            var interval = TimeSpan.FromHours(intervalHours);
            
            await _importService.ImportImdbDatasetAsync();
            
            await Task.Delay(interval, stoppingToken);
        }
    }
}
```

Registered in `Program.cs`:
```csharp
builder.Services.AddHostedService<ImdbImportBackgroundService>();
```

---

## Issue #25 — Quality pattern schema and seed data

### Comment to Post:

## Quality Pattern Schema — Design

Per #25 acceptance criteria, here is the schema for quality tokens and learned patterns.

### Entity Definitions

#### 1. QualityTokens (AC#1)
User-editable seed list of quality indicators.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| token_id | int | PK, IDENTITY | Unique token identifier |
| token_text | string | NOT NULL, UNIQUE | Quality token (e.g., "4K", "BluRay") |
| quality_rank | int | NOT NULL | Numeric rank (higher = better quality) |
| polarity | string | NOT NULL | Enum: positive, negative (HasConversion<string>()) |
| is_active | bool | NOT NULL, DEFAULT true | Whether token is currently evaluated |

**EF Core Configuration**:
```csharp
entity.HasKey(e => e.TokenId);

entity.Property(e => e.TokenText)
    .IsRequired()
    .HasMaxLength(100);

entity.HasIndex(e => e.TokenText)
    .IsUnique();

entity.Property(e => e.Polarity)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();

// Partial index via raw SQL (per #43)
migrationBuilder.Sql(@"
    CREATE INDEX IX_QualityTokens_IsActivePartial 
    ON QualityTokens (token_id) 
    WHERE is_active = true
");
```

#### 2. QualityLearnedPatterns (AC#2)
Runtime-accumulated quality patterns with hit tracking.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| pattern_id | int | PK, IDENTITY | Unique pattern identifier |
| pattern_regex | string | NOT NULL | Regex pattern for matching |
| derived_rank | int | NOT NULL | Inferred quality rank |
| hit_count | int | NOT NULL, DEFAULT 0 | Number of times pattern matched |
| last_matched_at | datetime | NULL | Timestamp of last match |
| is_active | bool | NOT NULL, DEFAULT true | Whether pattern is evaluated |
| algorithm_version | string | NOT NULL | Algorithm version that created this pattern |
| polarity | string | NOT NULL | Enum: positive, negative (HasConversion<string>()) |
| source | string | NOT NULL | Enum: Seed, Learned, User (HasConversion<string>()) |

**EF Core Configuration**:
```csharp
entity.HasKey(e => e.PatternId);

entity.Property(e => e.PatternRegex)
    .IsRequired()
    .HasMaxLength(500);

entity.Property(e => e.Polarity)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();

entity.Property(e => e.Source)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();

entity.Property(e => e.AlgorithmVersion)
    .IsRequired()
    .HasMaxLength(50);

// Partial index via raw SQL (per #43 and AC#3)
migrationBuilder.Sql(@"
    CREATE INDEX IX_QualityLearnedPatterns_IsActivePartial 
    ON QualityLearnedPatterns (pattern_id) 
    WHERE is_active = true
");
```

### Seed Data (AC#1, AC#4, AC#6)

Seeded via migration `HasData()` or `Up()`:

```csharp
modelBuilder.Entity<QualityToken>().HasData(
    // Positive polarity
    new QualityToken { TokenId = 1, TokenText = "2160p", QualityRank = 100, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 2, TokenText = "4K", QualityRank = 100, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 3, TokenText = "1080p", QualityRank = 80, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 4, TokenText = "720p", QualityRank = 60, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 5, TokenText = "480p", QualityRank = 40, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 6, TokenText = "BluRay", QualityRank = 70, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 7, TokenText = "WEB-DL", QualityRank = 50, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 8, TokenText = "HEVC", QualityRank = 65, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 9, TokenText = "x265", QualityRank = 65, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 10, TokenText = "x264", QualityRank = 60, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 11, TokenText = "HDR", QualityRank = 75, Polarity = "positive", IsActive = true },
    new QualityToken { TokenId = 12, TokenText = "SDR", QualityRank = 50, Polarity = "positive", IsActive = true },
    
    // Negative polarity (downranked)
    new QualityToken { TokenId = 13, TokenText = "AI-upscaled", QualityRank = -10, Polarity = "negative", IsActive = true },
    new QualityToken { TokenId = 14, TokenText = "AI upscale", QualityRank = -10, Polarity = "negative", IsActive = true }
);

// Seed initial patterns with algorithm_version (AC#6)
modelBuilder.Entity<QualityLearnedPattern>().HasData(
    new QualityLearnedPattern 
    { 
        PatternId = 1, 
        PatternRegex = @"\b2160p\b", 
        DerivedRank = 100, 
        HitCount = 0, 
        AlgorithmVersion = "1.0", 
        Polarity = "positive", 
        Source = "Seed", 
        IsActive = true 
    }
    // Additional seed patterns as needed
);
```

### Settings Integration (AC#5)

`QualityPruningThreshold` seeded in `Settings` table:
```csharp
modelBuilder.Entity<Setting>().HasData(
    new Setting 
    { 
        Key = "QualityPruningThreshold", 
        Value = "5", 
        Description = "Patterns with hit_count below this threshold are candidates for pruning",
        LastModifiedAt = DateTime.UtcNow
    }
);
```

---

## Issue #20 — EPIC: IMDB Integration

### Comment to Post:

## IMDB Integration — Complete Data Architecture

This epic (#20) encompasses the full canonical media layer, IMDB dataset import pipeline, and title matching engine. Here is the high-level data architecture.

### Canonical Media Layer (#21)
All application logic references `MediaTitles.media_id` — NEVER IMDB `tconst` directly.

**Entities**:
- `DataSources` (lookup: IMDB, CSFD, etc.)
- `MediaTitles` (canonical_title, year, type)
- `MediaTitleAliases` (localized/alternative titles)
- `MediaEpisodes` (season, episode_number)
- `MediaRatings` (rating, vote_count per source)
- `ImdbTitleDetails` (tconst + IMDB-specific fields)

**Key Design Decision**: The `tconst` identifier is stored in `ImdbTitleDetails` and is NEVER directly referenced from domain entities (`Posts`, `Watchlist`, `ScrapeRuns`, etc.). All references go through `MediaTitles.media_id` via FK constraints.

### IMDB Dataset Import Pipeline (#22)
Three-phase staging pattern:
1. Bulk import via Npgsql COPY → staging tables (no indexes, no FKs)
2. Upsert → live tables via `INSERT ... ON CONFLICT DO UPDATE`
3. Cleanup → truncate staging tables

**Datasets Imported**:
- `title.basics` → `MediaTitles`
- `title.akas` → `MediaTitleAliases`
- `title.episode` → `MediaEpisodes`
- `title.ratings` → `MediaRatings`

**Progress Tracking**: `DataSourceImportRuns` table tracks each import job (status, rows_imported, error_message).

**Scheduling**: Runs as `BackgroundService`, interval configured via `Settings.ImdbRefreshIntervalHours` (default: 24).

### Title Matching Engine (#23)
Matching algorithm (Developer task) will query:
- `MediaTitles` (canonical_title + year + type)
- `MediaTitleAliases` (fuzzy matching on alias_title)

Indexes optimized for:
- Exact match: `(canonical_title, year, type)`
- Fuzzy match: `(alias_title)`

### Extensibility (IMetadataSource interface from #4)
Future sources (CSFD, TMDB, etc.):
1. Add row to `DataSources` table
2. Create source-specific detail table (e.g., `CsfdTitleDetails`)
3. Implement `IMetadataSource` interface
4. Register in DI container

**No changes required to**:
- `MediaTitles` layer
- `Posts`, `Watchlist`, or other domain entities
- Matching engine (queries `MediaTitles` regardless of source)

---

This completes the Data Engineer schema design phase for the architecture gate.
