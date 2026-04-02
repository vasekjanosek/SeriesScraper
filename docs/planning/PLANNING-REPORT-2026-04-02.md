# Planner Agent Report — Planning Phase Complete
**Date**: 2026-04-02  
**Planner Agent**: GitHub Copilot (Planner Mode)  
**Phase Gate**: `gate:planning` active

---

## Executive Summary

**Reviewed**: 11 feature issues  
**Ready for development**: **3 issues** (#12, #23, #17)  
**Blocked**: 7 issues (dependencies not met)  
**Critical path bottleneck**: Issue #23 (IMDB matching engine) blocks 3 other high-priority issues  
**Circular dependency detected**: #19 ↔ #31 (resolution strategy provided)  
**Duplicate issue**: #34 and #36 are identical (action required)

---

## Issues Ready for Development (`status:ready`)

### Batch 1 — Start Immediately (All Dependencies Met)

#### 1. #12 — Forum section discovery and classification

**Status**: ✅ `status:ready`  
**Priority**: `priority:high` (unblocks #13, #16)  
**Dev Slot**: Recommend `dev-slot:1`

**Dependencies verified**:
- ✅ #3 — IForumScraper interface (merged PR #52)
- ✅ #6 — Language detection research (SearchPioneer.Lingua selected)
- ✅ #28 — Link type registry (merged PR #60)

**Implementation scope**:
- Create `Infrastructure/Services/ForumStructureLearningService.cs` — implements crawl/classification
- New EF migration → `ForumSections` table:
  - Fields: `section_id` (PK), `forum_id` (FK→Forums), `parent_section_id` (nullable self-FK), `url` (unique), `name`, `detected_language`, `content_type_id` (FK→ContentTypes), `last_crawled_at`, `is_active`
- New EF migration → `ContentTypes` lookup table with seed data: TV Series, Movie, Other
- Extend `Domain/Interfaces/IForumScraper.cs` → add `EnumerateSections(int depth)` method
- Use SearchPioneer.Lingua (from #6) for section name language detection and keyword-based classification

**Key requirements**:
- Incremental enrichment: new sections added, existing updated, missing marked inactive
- Respect `Forums.politeness_delay_ms` between HTTP requests
- Follow schema conventions from ADR-004 and #38

**Definition of Done**: All 7 acceptance criteria met

---

#### 2. #23 — IMDB title matching engine

**Status**: ✅ `status:ready`  
**Priority**: ⚠️ `priority:critical` — **BLOCKS** #16, #31, #19  
**Dev Slot**: Recommend `dev-slot:2` — **HIGHEST PRIORITY TASK**

**Dependencies verified**:
- ✅ #21 — Canonical media title layer (merged PR #57)
- ✅ #22 — IMDB import pipeline (merged PR #62)
- ✅ #4 — IMetadataSource interface (merged PR #52)

**Implementation scope**:
- Create `Application/Services/TitleMatchingService.cs` — implements matching logic
- Create `Application/Interfaces/ITitleMatchingService.cs` — follows `IMetadataSource` pattern (ADR-002)
- Schema change: add columns to scrape results table:
  - `match_confidence` (float, 0.0–1.0)
  - `match_status` (string: "Matched" / "Unmatched" / "Partial")
- Settings table seed: add `imdb_rematch_interval_hours` (default: 24)

**Matching algorithm**:
1. **Exact alias lookup**: Query `MediaTitleAliases` by normalized title → confidence = 1.0
2. **Fuzzy fallback**: If no exact match, use Levenshtein distance or trigram similarity → confidence = calculated score (Data Engineer choice)
3. **No match**: Store `match_status = "Unmatched"`, confidence = 0.0, and return result (do NOT filter out)

**Key requirements**:
- Matching engine MUST NOT reference `ImdbTitleDetails` or `tconst` directly — use `IMetadataSource` abstraction only (AC #5)
- Case-insensitive alias matching required
- Refresh interval stored in DB Settings, not `appsettings.json`
- Unit tests must cover: exact alias, case-insensitive, fuzzy match, no-match, and confidence score boundaries (AC #7)

**Definition of Done**: All 7 acceptance criteria met, including comprehensive unit test coverage

---

#### 3. #17 — Forum session management and re-authentication

**Status**: ✅ `status:ready`  
**Priority**: `priority:high` (core scraping infrastructure)  
**Dev Slot**: Recommend `dev-slot:3`

**Dependencies verified**:
- ✅ #3 — IForumScraper interface contract (merged PR #52)
- ✅ Serilog structured logging implemented (verified in codebase, credential destructuring policy active from #42/PR #56)

**Implementation scope**:
- Extend `Infrastructure/Services/{Concrete}ForumScraperService.cs` — your concrete `IForumScraper` implementation
- Configure `HttpClient` with a `CookieContainer` instance scoped to scrape run
- Implement session expiry detection (forum-specific: redirect to login or HTTP 401)
- Auto re-authenticate on expiry WITHOUT propagating exception to caller
- Logging:
  - INFO: re-auth attempts with structured fields: `{forum_id}`, `{run_id}`, `{attempt_number}`
  - ERROR: re-auth failure with structured fields: `{forum_id}`, `{run_id}`, `{error_message}`
- Failed re-auth → mark run status as `Failed`

**Key requirements**:
- Session persists across all HTTP requests within a single scraping session
- Re-authentication is scraper's responsibility — engine does NOT handle this
- Credentials (`username`, `password`) NEVER appear in logs (enforced by Serilog policy)
- Session state is NOT persisted across app restarts (each run starts fresh)

**Out of Scope**: Multi-factor authentication, CAPTCHA handling, session persistence across restarts

**Definition of Done**: All 5 acceptance criteria met

---

## Blocked Issues

### Circular Dependency (Resolution Required)

**Problem**:
- **#19** (Persistent watchlist page) depends on **#31** (Results page) for badge integration in results UI
- **#31** (Results page) depends on **#19** (Watchlist) for watchlist badge rendering logic

**Resolution Strategy** — Split #31 into two sub-tasks:

1. **Create #31a (NEW)** — Results page WITHOUT watchlist integration  
   Dependencies: #15 ✓, #23 (not ready), #26 ✓, #29 ✓  
   → Mark `status:ready` after #23 merges

2. **Create #31b (NEW)** — Add watchlist badges to Results page  
   Dependencies: #19 (Watchlist page), #31a (base Results page)  
   → Mark `status:ready` after #19 and #31a merge

**Execution order**: #23 → #31a → #19 → #31b

---

### Other Blocked Issues

#### #13 — Structure refresh scheduling
- **Blocked by**: #12 (not ready yet), #34/#36 (Settings page — blocked by #9)
- **Priority**: `priority:medium`
- **Mark ready when**: #12 merges AND #34/#36 resolves

#### #16 — Multi-item scrape run: search form and trigger
- **Blocked by**: #23 (not ready yet)
- **Priority**: `priority:high`
- **Mark ready when**: #23 merges

#### #32 — Run progress page
- **Blocked by**: #16 (not ready yet)
- **Priority**: `priority:high`
- **Mark ready when**: #16 merges

#### #33 — IMDB dataset status and app info
- **Blocked by**: #34/#36 (Settings page — blocked by #9)
- **Priority**: `priority:low`
- **Mark ready when**: #34/#36 resolves

#### #34 and #36 — Settings page structure (DUPLICATE ISSUES)
- **Issue**: Both #34 and #36 have identical titles ("Settings page structure and global settings") and bodies
- **Blocked by**: #9 (Forum CRUD — blocked by #49 needs-human decision)
- **Priority**: `priority:medium`
- **Action required**:
  1. Close #36 as duplicate of #34 (or vice versa)
  2. Keep one issue, update references in #13, #33, #35 (Settings epic)
  3. Wait for #49 human decision to unblock #9

---

## Batch Execution Plan (Max 3 Parallel Dev Slots)

### Batch 1 — Start NOW

| Slot | Issue | Priority | Estimated Duration |
|------|-------|----------|-------------------|
| `dev-slot:1` | #12 — Forum section discovery | high | 4-6 days |
| `dev-slot:2` | **#23 — IMDB matching engine** | **critical** | **5-7 days** |
| `dev-slot:3` | #17 — Forum session management | high | 3-5 days |

**Merge timeline**: 5-7 days (constrained by #23)

---

### Batch 2 — After #23 Merges

| Slot | Issue | Priority | Dependencies |
|------|-------|----------|--------------|
| `dev-slot:1` | #31a (NEW) — Results page (no watchlist) | high | #23 ✓, #15 ✓, #26 ✓, #29 ✓ |
| `dev-slot:2` | #16 — Multi-item scrape form | high | #23 ✓, #15 ✓ |
| `dev-slot:3` | Hold or #13 if unblocked | medium | #12 ✓, #34/#36 resolved |

**Merge timeline**: 5-7 days

---

### Batch 3 — After #12, #16, #31a Merge

| Slot | Issue | Priority | Dependencies |
|------|-------|----------|--------------|
| `dev-slot:1` | #32 — Run progress page | high | #16 ✓, #15 ✓ |
| `dev-slot:2` | #19 — Watchlist page | medium | #23 ✓, #31a ✓ |
| `dev-slot:3` | #13 — Structure refresh scheduling | medium | #12 ✓, #34/#36 ✓ |

**Merge timeline**: 5-7 days

---

### Batch 4 — Deferred (Low Priority or Blocked by Human Decision)

| Issue | Priority | Block Reason |
|-------|----------|--------------|
| #33 — IMDB dataset status UI | low | Blocked by #34/#36 → blocked by #9 |
| #34/#36 — Settings page | medium | Blocked by #9 → blocked by #49 needs-human |
| #31b (NEW) — Add watchlist badges | medium | Depends on #19 + #31a |

---

## Action Items

### Immediate Actions (Planner Agent)

1. ✅ Mark as `status:ready` + `agent:developer`:
   - #12 — Forum section discovery (`priority:high`, `dev-slot:1`)
   - #23 — IMDB matching engine (`priority:critical`, `dev-slot:2`) ⚠️ **PRIORITIZE THIS**
   - #17 — Forum session management (`priority:high`, `dev-slot:3`)

2. ❌ **Cannot auto-label** (GitKraken auth required) — Manual action or Orchestrator required:
   - Apply labels via GitHub API or web UI

### Required Actions (Product Manager / Orchestrator)

3. ⚠️ **Resolve duplicate issues**: #34 and #36 are identical
   - Close one as duplicate
   - Update references in #13, #33, Epic #35

4. ⚠️ **Break circular dependency**: Create sub-tasks for #31
   - #31a — Results page without watchlist (mark ready after #23 merges)
   - #31b — Add watchlist badges (mark ready after #19 + #31a merge)

5. ⚠️ **Human decision required**: Issue #49 blocks:
   - #9 (Forum CRUD)
   - #51 (Authentication policy)
   - #34/#36 (Settings page)
   - #33 (IMDB status UI)
   - Epic #35 (Settings)

   **Action**: Tag @vasekjanosek for #49 resolution

---

## Test Coverage Requirements

All tasks must meet **≥90% test coverage** (enforced by CI).

**Recommended test structure for each ready issue**:

### #12 — Forum section discovery
- **Unit tests**: Section classification logic, language detection, duplicate section handling, incremental enrichment, politeness delay enforcement
- **Integration tests** (Testcontainers): Crawl mock forum endpoint, verify DB inserts/updates, test incremental enrichment scenario (new/updated/inactive sections)

### #23 — IMDB matching engine
- **Unit tests** (AC #7):
  - Exact alias match → confidence = 1.0
  - Case-insensitive alias match → confidence = 1.0
  - Fuzzy match with known input → confidence = calculated score (e.g., 0.85)
  - No match → `match_status = "Unmatched"`, confidence = 0.0
  - Edge cases: empty title, special characters, very long titles
  - Confidence boundaries: 0.0, 1.0, intermediate values
- **Integration tests**: Match against real `MediaTitleAliases` data (seed test IMDB subset)

### #17 — Forum session management
- **Unit tests** (mocked `HttpClient`): Session expiry detection, re-auth invoked, exception not propagated to caller
- **Integration tests** (Testcontainers + mock forum endpoint): Simulate session expiry mid-scrape, verify re-auth succeeds, run continues without error
- **Log assertions**: Verify credentials redacted in logs, structured fields present (`{forum_id}`, `{run_id}`, `{attempt_number}`)

---

## Architecture Alignment

All implementations must follow:
- **ADR-001**: Clean Architecture (4-project solution structure)
- **ADR-002**: Interface contracts (`IForumScraper`, `IMetadataSource`, `ILinkParser`)
- **ADR-003**: Scraping engine architecture (background service pattern)
- **ADR-004**: Data model architecture (EF Core conventions, schema naming)

**Dependency rule** (must never violate):
```
Web → Infrastructure → Application → Domain
```

**Reminder**: Domain layer has ZERO NuGet dependencies. Only C# primitives and interface definitions.

---

## Summary

**Ready now**: 3 issues (#12, #23, #17) — all dependencies met, clear implementation guidance provided  
**Next batch**: 3 issues (#31a, #16, #13) — will be ready after #23 merges  
**Blocked**: 4 issues (#32, #19, #33, #34/#36) — require dependency resolution or human decision  
**Critical path**: Issue #23 is the highest-priority bottleneck — blocks 3 other tasks

**Recommended action**: Start Batch 1 immediately with #23 as top priority.
