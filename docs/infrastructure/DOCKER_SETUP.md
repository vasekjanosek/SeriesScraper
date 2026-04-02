# Docker Setup Guide

## Overview

SeriesScraper uses Docker Compose to orchestrate the application stack for local development and production deployment.

## Architecture

The stack consists of three services:

1. **app** - Blazor Server application (.NET 8)
2. **db** - PostgreSQL 16 database (Alpine-based)
3. **github-runner** - Self-hosted GitHub Actions runner for CI/CD

## Prerequisites

- Docker Engine 20.10+ or Docker Desktop
- Docker Compose V2+
- At least 15-30 GB free disk space for IMDB datasets and PostgreSQL data
- GitHub Personal Access Token (for runner registration)

## Quick Start

### 1. Environment Configuration

Copy the example environment file and configure it:

```bash
cp .env.example .env
```

**CRITICAL**: Edit `.env` and set:

- `DB_PASSWORD` - Use a strong random password (minimum 32 characters recommended)
  ```bash
  # Generate with:
  openssl rand -base64 32
  ```
- `GITHUB_RUNNER_TOKEN` - From GitHub Settings → Actions → Runners → New runner
- `FORUM_USERNAME` and `FORUM_PASSWORD` - Your forum credentials
- `APP_ENV` - Set to `Development` for local dev, keep `Production` for deployment

### 2. Start Services

```bash
# Start all services in detached mode
docker compose up -d

# View logs
docker compose logs -f

# View logs for specific service
docker compose logs -f app
```

### 3. Verify Health

```bash
# Wait for services to start (60 seconds for app health check start period)
sleep 60

# Check health status
docker compose ps

# Test health check endpoint
curl http://localhost:8080/healthz
```

### 4. Access Services

- **Application**: http://localhost:8080
- **PostgreSQL**: `localhost:5432` (from host only - bound to 127.0.0.1)
  - Username: `scraper` (configurable via `DB_USER`)
  - Password: From `DB_PASSWORD` in `.env`
  - Database: `seriescraper` (configurable via `DB_NAME`)

## Service Details

### Application Service

- **Image**: Built from `Dockerfile` (multi-stage .NET 8 Alpine build)
- **Port**: 8080 (configurable via `APP_PORT`)
- **Binding**: 127.0.0.1 only by default (configurable via `APP_BIND_ADDRESS`)
- **Health Check**: `/healthz` endpoint verifies DB connectivity
- **Volumes**:
  - `imdb-datasets`: IMDB TSV files (5-15 GB)
  - `app-logs`: Application logs from Serilog

### Database Service

- **Image**: `postgres:16-alpine` (pinned tag, never `:latest`)
- **Port**: 5432 (bound to 127.0.0.1 only - NOT accessible from LAN)
- **Health Check**: `pg_isready` command
- **Volume**: `postgres_data` (5-15 GB depending on IMDB dataset)
- **Security**: Loopback binding prevents network exposure

### GitHub Actions Runner

- **Image**: `myoung34/github-runner:latest`
  - ⚠️ TODO: Replace with pinned digest SHA before production use
  - Find digest: `docker pull myoung34/github-runner:latest && docker inspect --format='{{index .RepoDigests 0}}' myoung34/github-runner:latest`
- **Purpose**: Runs CI workflows locally
- **Token**: Expires after 1 hour - regenerate if runner fails to register
- **Volume**: `runner_work` (build artifacts, typically <2 GB)

## Disk Usage

Expected disk usage by volume:

| Volume | Purpose | Size |
|--------|---------|------|
| `postgres_data` | PostgreSQL database files | 5-15 GB |
| `imdb-datasets` | IMDB TSV files and cache | 5-15 GB |
| `app-logs` | Application logs (rotated) | <1 GB |
| `runner_work` | CI build artifacts | <2 GB |
| **Total** | | **15-33 GB** |

## Security Considerations

⚠️ **THREAT MODEL**: This application is designed for LOCAL-NETWORK USE ONLY. It MUST NOT be exposed to the public internet.

Security measures implemented:

1. **PostgreSQL Loopback Binding**
   - Port `5432` bound to `127.0.0.1` only
   - NOT accessible from LAN or external networks
   - Safe for local development with tools like pgAdmin, DBeaver

2. **Application Binding**
   - Default binding: `127.0.0.1:8080` (localhost only)
   - Configurable via `APP_BIND_ADDRESS` and `ASPNETCORE_URLS`
   - Change to `0.0.0.0` only for trusted local networks

3. **Strong Password Requirement**
   - `.env.example` has explicit placeholder: `CHANGE_ME_USE_STRONG_RANDOM_PASSWORD`
   - Application should fail to start with weak passwords

4. **Environment Variable Isolation**
   - All secrets in `.env` file (git-ignored)
   - Never commit `.env` to version control

5. **Non-Root Container Execution**
   - Application runs as `appuser` (UID 1001)
   - Not running as root inside container

### Additional Hardening for Public Deployment

If deploying beyond localhost (NOT RECOMMENDED):

- Add TLS termination (reverse proxy: nginx, Traefik, Caddy)
- Implement authentication/authorization
- Enable rate limiting
- Add web application firewall (WAF)
- Use secrets management (HashiCorp Vault, Azure Key Vault)
- Implement network segmentation
- Enable audit logging

## Common Operations

### Stopping Services

```bash
# Stop all services
docker compose down

# Stop and remove volumes (CAUTION: deletes all data)
docker compose down -v
```

### Restarting a Single Service

```bash
# Restart just the app
docker compose restart app

# Rebuild and restart app after code changes
docker compose up -d --build app
```

### Viewing Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f db

# Last 100 lines
docker compose logs --tail=100 app
```

### Database Access

```bash
# Connect to PostgreSQL using psql
docker compose exec db psql -U scraper -d seriescraper

# Run SQL file
docker compose exec -T db psql -U scraper -d seriescraper < backup.sql

# Create backup
docker compose exec db pg_dump -U scraper seriescraper > backup.sql
```

### Updating Database Password

1. Stop all services: `docker compose down`
2. Update `DB_PASSWORD` in `.env`
3. Remove PostgreSQL volume: `docker volume rm seriescraper_postgres_data`
4. Restart services: `docker compose up -d`

### Inspecting Volumes

```bash
# List all volumes
docker volume ls

# Inspect volume details
docker volume inspect seriescraper_postgres_data

# Check disk usage
docker system df -v
```

## Troubleshooting

### Service Won't Start

```bash
# Check logs for errors
docker compose logs app

# Verify environment variables
docker compose config
```

### Health Check Failing

```bash
# Check app health manually
curl -v http://localhost:8080/healthz

# Check database connectivity from app container
docker compose exec app curl -f http://localhost:8080/healthz
```

### Database Connection Refused

1. Verify PostgreSQL is running: `docker compose ps db`
2. Check health: `docker compose exec db pg_isready -U scraper -d seriescraper`
3. Verify connection string in app environment
4. Check `DB_PASSWORD` matches in both services

### GitHub Runner Not Registering

1. Verify `GITHUB_RUNNER_TOKEN` is correct and not expired (tokens expire after 1 hour)
2. Regenerate token: GitHub → Settings → Actions → Runners → New self-hosted runner
3. Update `.env` with new token
4. Restart runner: `docker compose restart github-runner`
5. Check runner logs: `docker compose logs github-runner`

### Out of Disk Space

Check volume usage:

```bash
docker system df -v
```

Clean up unused containers and images:

```bash
# Remove unused images
docker image prune -a

# Remove unused volumes (CAUTION: may delete data)
docker volume prune
```

## CI/CD Integration

The self-hosted runner automatically registers with the GitHub repository and runs workflows with the `self-hosted` label.

Example workflow step:

```yaml
jobs:
  ci:
    runs-on: self-hosted  # Uses the docker compose runner
    steps:
      - uses: actions/checkout@v4
      - name: Build and Test
        run: dotnet test --configuration Release
```

## References

- Docker Compose Documentation: https://docs.docker.com/compose/
- PostgreSQL Official Docker Image: https://hub.docker.com/_/postgres
- ASP.NET Core Docker Images: https://hub.docker.com/_/microsoft-dotnet-aspnet
- GitHub Self-Hosted Runners: https://github.com/myoung34/docker-github-actions-runner

## Related Documentation

- [Health Check Implementation](./HEALTHCHECK_IMPLEMENTATION.md)
- [Database Migrations](../database/MIGRATIONS.md) (if available)
- ADR-001: System Architecture (if available)
- Issue #38: Docker Compose setup
