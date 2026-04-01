# Shared Agent Instructions

> **SYNCHRONIZATION RULE — CRITICAL**
> This file is the single source of truth for all agent instructions.
> Any change made here MUST be immediately propagated to ALL platform-specific files:
> - `CLAUDE.md` (Claude Code)
> - `AGENTS.md` (Codex CLI)
> - `.github/copilot-instructions.md` (GitHub Copilot)
>
> Any agent that modifies this file is responsible for updating all three platform files
> to match. Instructions that exist in one platform file must exist in all of them.

---

## Project Context

```
PROJECT_NAME:    SeriesScraper
GITHUB_REPO:     https://github.com/vasekjanosek/SeriesScraper
TECH_STACK:      C#/.NET 8, Blazor (frontend), PostgreSQL 16, Docker Compose
DESCRIPTION:     Private web application that scrapes a forum (authenticated) for direct
                 download links to movies and TV series. Cross-checks links against IMDB
                 datasets to verify metadata (episode count, titles, ratings). Evaluates
                 relevance of scraped content. Data source architecture is extensible —
                 IMDB is the initial source; CSFD and others can be added as plugins.
                 Scraping speed is user-configurable from the UI.
MAX_DEV_SLOTS:   3
GITHUB_USERNAME: vasekjanosek
```

---

## Agent Roster

| Agent | Role | Trigger |
|---|---|---|
| `orchestrator` | Coordinates all agents, manages pipeline state | User prompt |
| `product-manager` | Scope, acceptance criteria, PR approval, merge | User scope prompt / Orchestrator |
| `architect` | System design, tech decisions, ADRs | Orchestrator after PM scope |
| `planner` | Breaks epics into tasks, assigns priority and dev slots | Orchestrator after architecture |
| `developer` | Implements features, fixes review/test failures | Orchestrator (up to MAX_DEV_SLOTS parallel) |
| `reviewer` | Code quality review | Orchestrator after PR opened |
| `tester` | Static analysis + dynamic testing | Orchestrator after review passes |
| `ux-designer` | HTML/CSS wireframe prototypes | Orchestrator for UI features (parallel with architect) |
| `devops` | Docker, CI/CD, infrastructure | Orchestrator during setup and infra tasks |
| `security` | Static + dynamic security testing | Orchestrator after tester |
| `data-engineer` | Schema design, data pipelines | Orchestrator during architecture phase |
| `research` | Technology research, evaluations | On-demand by any agent |
| `evaluator` | Monitors conflict cycles, detects loops | Orchestrator after 20 conflict cycles |

---

## Pipeline Sequence

```
User triggers Orchestrator
        │
        ▼
  Orchestrator reads GitHub Issues state → determines phase
        │
        ├─── [PHASE: scope] ──────────────► PM Agent
        │                                        │ Creates issues with acceptance criteria
        │                                        │ Labels: gate:architecture
        │
        ├─── [PHASE: architecture] ──────► Architect + UX Designer (parallel for UI)
        │                                  + Data Engineer (schema)
        │                                        │ Labels: gate:planning
        │
        ├─── [PHASE: planning] ──────────► Planner
        │                                        │ Breaks epics, sets status:ready
        │
        └─── [PHASE: development] ───────► Orchestrator assigns issues to dev slots
                                                 │
                                          Developer(s) [max MAX_DEV_SLOTS parallel]
                                                 │ Creates feature/issue-{n}-{slug} branch
                                                 │ Implements, opens PR
                                                 │
                                          Reviewer
                                                 │ Code quality → status:in-testing or back to developer
                                                 │
                                          Tester
                                                 │ Tests (≥90% coverage) → status:awaiting-pm or back
                                                 │
                                          PM Agent
                                                 │ Acceptance criteria → status:pm-approved or back
                                                 │
                                          Merge Gate Workflow
                                                 │ CI passed + status:pm-approved → PM agent merges
                                                 ▼
                                              main branch
```

---

## GitHub Issues State Machine

Agents communicate exclusively through GitHub Issues labels and PR comments.

### Picking Up Work
Each agent scans for issues matching its label + correct status:
- Developer picks up: `agent:developer` + `status:ready`
- Reviewer picks up: open PR + `status:in-review`
- Tester picks up: PR + `status:in-testing`
- PM picks up: PR + `status:awaiting-pm`

### Label Schema

**Status**
| Label | Meaning |
|---|---|
| `status:backlog` | Created, not yet planned |
| `status:ready` | Planned, ready for agent pickup |
| `status:in-progress` | Agent actively working |
| `status:in-review` | Under Reviewer agent |
| `status:in-testing` | Under Tester agent |
| `status:awaiting-pm` | Waiting for PM acceptance |
| `status:pm-approved` | PM approved, merge triggered |
| `status:done` | Merged and closed |
| `status:blocked` | Waiting on dependency |
| `status:on-hold` | Paused by user decision |

**Agent Assignment**
`agent:orchestrator` / `agent:pm` / `agent:architect` / `agent:planner` /
`agent:developer` / `agent:reviewer` / `agent:tester` / `agent:ux` /
`agent:devops` / `agent:security` / `agent:data-engineer` / `agent:research` /
`agent:evaluator`

**Developer Slots** (parallel dev tracking)
`dev-slot:1` / `dev-slot:2` / `dev-slot:3`

**Type**
`type:epic` / `type:feature` / `type:bug` / `type:task` /
`type:research` / `type:design` / `type:infrastructure` / `type:security`

**Priority**
`priority:critical` / `priority:high` / `priority:medium` / `priority:low`

**Gates**
`gate:scope` / `gate:architecture` / `gate:planning`

**Special**
| Label | Meaning |
|---|---|
| `needs-human` | Requires @vasekjanosek — comment will tag them |
| `conflict-loop` | Stuck after conflict cycle limit |

---

## Conflict Resolution Protocol

1. Developer implements → opens PR
2. Reviewer or Tester rejects → Developer fixes → force-pushes to feature branch → cycle repeats
3. After **20 cycles**: Evaluation Agent is invoked
4. Evaluation Agent checks:
   - Is the code diff growing between cycles? (new code being produced)
   - Is the test pass rate improving between cycles?
5. If **making progress**: Evaluation Agent allows a new 20-cycle batch to begin
6. If **stuck / looping**: Add `conflict-loop` + `needs-human` labels, post comment tagging @vasekjanosek
7. Force-push is ALWAYS to the feature branch — NEVER to `main`

---

## Merge Protocol (Label-Based Gate)

1. PM Agent reviews PR against acceptance criteria
2. If approved: PM Agent adds `status:pm-approved` label to the PR
3. GitHub Actions `merge-gate.yml` workflow triggers on label add
4. Workflow verifies all CI checks have passed
5. PM Agent calls GitHub API (`POST /repos/vasekjanosek/SeriesScraper/pulls/{number}/merge`) to execute merge
   - merge_method: `squash`
6. Branch is deleted after merge
7. Linked issue is closed automatically (via `Closes #n` in PR body)

---

## Branch Strategy (GitHub Flow)

- All work happens on feature branches: `feature/issue-{number}-{short-slug}`
- PRs target `main` only
- `main` is protected:
  - Direct pushes blocked
  - CI must pass before merge
  - Merge only via PM Agent through API (label gate)
- No `develop` branch

---

## Tech Stack Notes

- **Backend**: C#/.NET 8 — use minimal API or controller-based REST as architect decides
- **Frontend**: Blazor Server or Blazor WebAssembly — architect decides based on requirements
- **Database**: PostgreSQL 16 via Entity Framework Core with migrations
- **ORM**: Entity Framework Core — all schema changes via versioned migrations
- **Testing**: xUnit, FluentAssertions, Testcontainers (for integration tests with real PostgreSQL)
- **Coverage**: Coverlet + ReportGenerator — minimum 90% enforced in CI
- **Static analysis**: SonarAnalyzer.CSharp or Roslyn analyzers
- **Containerization**: Docker Compose — all services defined in `docker-compose.yml`

## Data Source Architecture

All external data sources MUST implement a common interface (defined by Architect/Data Engineer).
New sources (CSFD, etc.) are added as plugins without modifying existing code.
IMDB dataset refresh interval is user-configurable (stored in DB), default: 24 hours.

## Scraping Rules

- Forum scraping uses authenticated sessions (credentials from .env)
- Scraping speed is configurable from the UI (min/max delay between requests)
- Absolute speed limits are defined by PM agent during scope definition
- Scraper backend is designed to be pluggable (forum software TBD)

---

## GitHub Copilot Note

GitHub Copilot (VS Code extension) cannot execute agents or run automated pipelines.
It reads `.github/copilot-instructions.md` as passive context only.
Automation is handled exclusively by Claude Code and Codex CLI.
