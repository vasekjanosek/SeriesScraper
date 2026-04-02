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
| `planner` | Reviews and refines PM's tasks, splits further, adds detail | Orchestrator after architecture |
| `developer` | Implements features, fixes review/test failures | Orchestrator (up to MAX_DEV_SLOTS parallel) |
| `reviewer` | Code quality review | Orchestrator after PR opened |
| `tester` | Static analysis + dynamic testing | Orchestrator after review passes |
| `ux-designer` | HTML/CSS wireframe prototypes | Orchestrator for UI features (parallel with architect) |
| `devops` | Docker, CI/CD, infrastructure | Orchestrator during setup and infra tasks |
| `security` | Design review after architecture + code review after tester | Orchestrator (two passes) |
| `data-engineer` | Schema design, data pipelines | Orchestrator during architecture phase |
| `research` | Technology research, evaluations | Orchestrator BEFORE architecture (blocking) |
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
        │                                        │ Creates epics + task issues with acceptance criteria
        │                                        │ Labels: gate:research
        │
        ├─── [PHASE: research] ──────────► Research Agent
        │                                        │ Runs BEFORE architecture — outcomes inform design
        │                                        │ Completes all type:research issues
        │                                        │ Labels: gate:architecture
        │
        ├─── [PHASE: architecture] ──────► Architect + UX Designer (parallel for UI)
        │                                  + Data Engineer (schema)
        │                                        │ Uses research outcomes
        │                                        │ Labels: gate:security-review
        │
        ├─── [PHASE: security-review] ──► Security Agent (PASS 1: design review)
        │                                        │ Reviews architecture for threats
        │                                        │ Re-evaluates existing security issues
        │                                        │ Creates new issues if needed
        │                                        │ Labels: gate:planning
        │
        ├─── [PHASE: planning] ──────────► Planner
        │                                        │ Reviews and refines PM's existing tasks
        │                                        │ Splits further, adds implementation detail
        │                                        │ Sets status:ready on tasks
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
                                                 │ Tests (≥90% coverage) → status:security-review or back
                                                 │
                                          Security Agent (PASS 2: code review)
                                                 │ Reviews code for vulnerabilities
                                                 │ → status:awaiting-pm or back to developer
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
- Research picks up: `agent:research` + `status:backlog` (during research phase)
- Developer picks up: `agent:developer` + `status:ready`
- Reviewer picks up: open PR + `status:in-review`
- Tester picks up: PR + `status:in-testing`
- Security picks up: PR + `status:security-review` (pass 2: code review)
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
| `status:security-review` | Under Security agent (code review) |
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
`gate:scope` / `gate:research` / `gate:architecture` / `gate:security-review` / `gate:planning`

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

## Global Precision & Thoroughness Standards

**These standards apply to every agent without exception. No agent may skip or abbreviate any step below.**

### Before Starting Any Task
- Read ALL relevant context in full: the assigned issue, all linked issues, the parent epic, the architecture ADR, and any referenced documents
- Do not begin work based on a partial or skimmed reading
- If the task references other issues, read those issues too before starting
- If anything is ambiguous after reading, post a clarifying comment and wait — do not assume

### Research Standards
- Every research task must evaluate a minimum of 3 options or sources
- Verify all claims against primary sources: official documentation, source code, or official changelogs — not blog posts or tutorials
- Research must be specific to this project's tech stack and constraints, not generic recommendations
- If a finding contradicts the original assumption behind the research request, report that explicitly before concluding

### Implementation Standards
- Read every file that will be touched before writing a single line of code
- Understand the existing code pattern fully before adding to it
- Implement exactly what is specified — no approximations, no "close enough"
- Never leave partial implementations, TODO stubs, or placeholder logic unless the issue explicitly permits it
- Every implementation must include complete tests before submitting for review

### Review & Verification Standards
- Check every acceptance criterion explicitly against the actual code — do not assume a criterion is met
- Test every code path the implementation touches, not just the happy path
- When something looks correct, verify it is correct — do not approve on appearance alone
- If any part of the work is unclear or incomplete, reject and explain specifically what is missing

### Communication Standards
- All comments, reports, and approvals must be specific and evidence-based
- Vague statements ("looks good", "seems correct", "this is wrong") are not acceptable
- Every rejection must list each specific issue with: what it is, where it is (file + line), and what the correct behaviour should be
- Every approval must list what was verified and how

### When in Doubt
- Do more investigation, not less
- Post a question as a GitHub Issue comment rather than proceeding with an assumption
- If a task is larger or more complex than the issue suggests, report that before attempting a partial solution
- Incomplete work submitted for review wastes cycles — it is always better to ask first

---

## GitHub Copilot Note

GitHub Copilot CLI reads `.github/copilot-instructions.md` and can execute agents
and run automated pipelines, the same as Claude Code and Codex CLI.
