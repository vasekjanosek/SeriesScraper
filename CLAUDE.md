# Claude Code — SeriesScraper

This file provides Claude Code with project context and agent instructions.

> **Full agent instructions are in [SHARED_AGENTS.md](SHARED_AGENTS.md).**
> Read that file before taking any action in this project.
> If you modify SHARED_AGENTS.md, you must also update AGENTS.md and
> .github/copilot-instructions.md to match.

---

## Project

**SeriesScraper** — Web application (C#/.NET 8 + Blazor + PostgreSQL) that scrapes
an authenticated forum for direct download links to movies/TV series, cross-checks
them against IMDB datasets, and evaluates relevance. Data source architecture is
extensible (IMDB now, CSFD and others in future).

- GitHub: https://github.com/vasekjanosek/SeriesScraper
- Stack: C#/.NET 8, Blazor, PostgreSQL 16, Docker Compose
- CI: Self-hosted GitHub Actions runner (local Docker)
- Coverage minimum: 90%

## Quick Reference

- Agents live in `.claude/agents/`
- All work tracked via GitHub Issues (labels + comments)
- Branch strategy: GitHub Flow (`feature/issue-{n}-{slug}` → `main`)
- Merge: label-based gate (`status:pm-approved` + CI pass → PM agent merges via API)
- Conflict limit: 20 cycles, then Evaluation Agent
- Max parallel developers: 3

## Triggering the Orchestrator

When the user prompts you to start or resume:
1. Read GitHub Issues at https://github.com/vasekjanosek/SeriesScraper/issues
2. Determine current project phase from labels
3. Resume automatically from that phase
4. Delegate to the appropriate sub-agent(s) from `.claude/agents/`

Available agents: `orchestrator`, `product-manager`, `architect`, `planner`,
`developer`, `reviewer`, `tester`, `ux-designer`, `devops`, `security`,
`data-engineer`, `research`, `evaluator`

## Rules

- Never push directly to `main`
- Never force-push to `main` (only to feature branches)
- Always follow the Reviewer → Tester → PM approval sequence
- Always read SHARED_AGENTS.md before spawning sub-agents
- Secrets are in `.env` — never commit `.env` to git
- All schema changes via EF Core migrations — never modify DB directly
- IMDB datasets: non-commercial use only
