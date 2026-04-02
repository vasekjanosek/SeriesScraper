# GitHub Copilot — SeriesScraper

This file provides GitHub Copilot with passive project context for code suggestions.

> **Note:** Copilot cannot run automated pipelines. Automation is handled by
> Claude Code and Codex CLI. This file is for passive context only.
>
> Full agent instructions are in [SHARED_AGENTS.md](../SHARED_AGENTS.md).
> If you modify SHARED_AGENTS.md, you must also update CLAUDE.md and AGENTS.md.

---

## Project Overview

**SeriesScraper** is a private web application that:
1. Scrapes an authenticated forum for direct download links to movies and TV series
2. Cross-checks scraped links against IMDB datasets (episode count, titles, metadata)
3. Evaluates the relevance of scraped content
4. Presents results through a Blazor web UI

The data source architecture is extensible — IMDB is the initial source; CSFD and other
databases can be added as plugins implementing a common interface.

Tech stack: C#/.NET 8, Blazor, PostgreSQL 16, Docker Compose, xUnit, EF Core

## Architecture Principles

- Pluggable data sources via common interface (IDataSource or equivalent)
- Pluggable forum scrapers (forum software is configurable)
- Repository pattern for data access
- Clear layer separation: Web (Blazor) → Application → Domain → Infrastructure
- Docker Compose for local development (app + PostgreSQL + self-hosted CI runner)
- All schema changes via versioned EF Core migrations — never modify DB directly

## Code Conventions

- Follow existing patterns in the codebase
- Minimum 90% test coverage for all new code
- No hardcoded secrets — use IConfiguration / environment variables
- No direct pushes to `main` — always use feature branches via PRs
- IMDB datasets: non-commercial use only

## Agent Workflow (for context)

This project uses 13 AI sub-agents for development:
orchestrator, product-manager, architect, planner, developer, reviewer,
tester, ux-designer, devops, security, data-engineer, research, evaluator

Communication between agents happens via GitHub Issues labels and PR comments.
Pipeline: PM scope → Research → Architecture → Security design review → Planning → Development.
PR cycle: Developer → Reviewer → Tester → Security code review → PM approval.
Merge requires: CI pass + full PR cycle + PM approval (status:pm-approved label).
