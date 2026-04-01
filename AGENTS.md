# Codex CLI — SeriesScraper

This file provides Codex CLI with project context and agent instructions.

> **Full agent instructions are in [SHARED_AGENTS.md](SHARED_AGENTS.md).**
> Read that file before taking any action in this project.
> If you modify SHARED_AGENTS.md, you must also update CLAUDE.md and
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

- All work tracked via GitHub Issues (labels + comments)
- Branch strategy: GitHub Flow (`feature/issue-{n}-{slug}` → `main`)
- Merge: label-based gate (`status:pm-approved` + CI pass → PM agent merges via API)
- Conflict limit: 20 cycles, then Evaluation Agent
- Max parallel developers: 3

## Agent Roles

See SHARED_AGENTS.md for the full roster of 13 agents and their responsibilities.

Pipeline: Orchestrator → PM → Architect (+UX, +Data Engineer) → Planner → Developer(s) → Reviewer → Tester → PM approval → merge

## Triggering

When the user prompts you to start or resume:
1. Read GitHub Issues to determine current project phase from labels
2. Resume automatically from current state
3. Delegate to appropriate agent role as described in SHARED_AGENTS.md

## Rules

- Never push directly to `main`
- Never force-push to `main` — only to feature branches
- Always follow Reviewer → Tester → PM sequence before merge
- Secrets are in `.env` — never commit `.env`
- 90% minimum test coverage enforced in CI
- IMDB datasets: non-commercial use only
