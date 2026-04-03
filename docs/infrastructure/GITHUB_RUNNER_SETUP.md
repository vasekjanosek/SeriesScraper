# GitHub Actions Self-Hosted Runner Setup

## Overview

SeriesScraper uses a self-hosted GitHub Actions runner to execute CI/CD workflows locally. This approach provides:

- Full control over the CI environment
- Access to local resources (PostgreSQL, Docker)
- No GitHub Actions minutes consumption
- Consistent environment with production deployment

## Architecture

The runner is deployed as a Docker container within the docker-compose stack:

- **Image**: `myoung34/github-runner:2.320.0` (pinned for reproducibility)
- **Configuration**: Docker-outside-of-Docker (DooD) with host socket mount
- **Scope**: Repository-level runner (not organization or enterprise)
- **Labels**: `self-hosted` (matches `runs-on` in workflow files)
- **Network**: Connected to `seriescraper-network` for service communication

## Security Model

### Critical Security Warnings

⚠️ **ROOT-EQUIVALENT ACCESS**: The runner has unrestricted access to the host Docker daemon via `/var/run/docker.sock`. This grants:
- Ability to start containers with `--privileged` flag
- Mount arbitrary host directories
- Access host network and processes
- Modify or delete any container/volume/network

⚠️ **TRUSTED ENVIRONMENT ONLY**: 
- **NEVER** expose this runner to public repositories
- **NEVER** allow untrusted users to trigger workflows
- **ALWAYS** review workflow changes before running
- Consider the runner as having root access to your development machine

### Threat Model

| Threat | Impact | Mitigation |
|--------|--------|------------|
| Malicious workflow code | Full host compromise | Run only on private repos with trusted collaborators |
| Stolen `GITHUB_ACCESS_TOKEN` | Unauthorized runner registration | Rotate PAT monthly, use fine-grained tokens with minimal scope |
| Container breakout | Host system access | Keep Docker Engine updated, monitor CVEs |
| Resource exhaustion | DoS of host machine | Set container resource limits (future enhancement) |

## Prerequisites

### 1. GitHub Personal Access Token (PAT)

The runner requires a fine-grained Personal Access Token for persistent registration.

**Required Permissions**:
- **Repository**: `Actions` (Read and Write)
- **Repository**: `Administration` (Read and Write)

**Token Generation**:
1. Navigate to GitHub Settings → Developer settings → Personal access tokens → Fine-grained tokens
2. Click "Generate new token"
3. Configure:
   - **Token name**: `SeriesScraper Self-Hosted Runner`
   - **Expiration**: 90 days (GitHub maximum)
   - **Repository access**: Only select repositories → `vasekjanosek/SeriesScraper`
   - **Permissions**:
     - Actions: Read and write
     - Administration: Read and write
4. Click "Generate token"
5. **IMMEDIATELY** copy the token — you cannot view it again
6. Store in `.env` as `GITHUB_ACCESS_TOKEN=ghp_...`

**Token Rotation**: Set a calendar reminder for 80 days to regenerate before expiration.

### 2. Docker Engine Configuration

**Linux**: Ensure the Docker socket is accessible at `/var/run/docker.sock` with mode `0660` or `0666`.

```bash
# Verify socket exists and is accessible
ls -l /var/run/docker.sock
# Expected: srw-rw---- 1 root docker 0 Apr  3 10:00 /var/run/docker.sock

# If permission denied, add your user to the docker group
sudo usermod -aG docker $USER
newgrp docker
```

**Windows/Mac**: Docker Desktop automatically creates the socket mapping — no action required.

### 3. Environment Variables

Copy `.env.example` to `.env` and configure the following runner-specific variables:

```bash
# ─── GitHub Runner ───────────────────────────────────────────────────────────
GITHUB_REPO_URL=https://github.com/vasekjanosek/SeriesScraper
GITHUB_ACCESS_TOKEN=ghp_your_fine_grained_pat_here
RUNNER_NAME=seriescraper-runner

# ─── Testcontainers ──────────────────────────────────────────────────────────
# Docker 20.10+ on Linux: host-gateway
# Windows/Mac: host.docker.internal
TESTCONTAINERS_HOST_OVERRIDE=host-gateway

# Disable Ryuk only if you manually clean up test containers
TESTCONTAINERS_RYUK_DISABLED=false
```

## Initial Setup

### 1. Start the Runner

```bash
# Start all services including the runner
docker compose up -d

# View runner logs to verify registration
docker compose logs -f github-runner
```

**Expected Output**:
```
github-runner  | Runner successfully registered
github-runner  | Runner.Listener started
```

### 2. Verify Registration in GitHub

1. Navigate to: https://github.com/vasekjanosek/SeriesScraper/settings/actions/runners
2. Look for runner with name `seriescraper-runner`
3. Verify status: **Idle** (green circle)

**Troubleshooting**: If runner shows **Offline**:
- Check logs: `docker compose logs github-runner`
- Verify `GITHUB_ACCESS_TOKEN` has correct permissions
- Ensure `GITHUB_REPO_URL` matches your repository URL exactly
- Token may have expired — regenerate and update `.env`

### 3. Test with a Workflow Trigger

```bash
# Trigger CI workflow by pushing to a feature branch
git checkout -b test/runner-verification
git commit --allow-empty -m "test: Verify self-hosted runner"
git push origin test/runner-verification
```

Verify in GitHub Actions:
- Navigate to: https://github.com/vasekjanosek/SeriesScraper/actions
- Check that the workflow runs on your runner (logs show "self-hosted" label)
- Workflow should complete successfully

## Health Check

The runner container includes a health check that verifies the `Runner.Listener` process is running.

```bash
# Check health status
docker compose ps github-runner

# Expected output:
# NAME                  STATUS                    PORTS
# github-runner         Up 2 minutes (healthy)
```

**Health Check Details**:
- **Command**: `pgrep -f Runner.Listener || exit 1`
- **Interval**: Every 30 seconds
- **Timeout**: 10 seconds per check
- **Retries**: 3 consecutive failures before marking unhealthy
- **Start Period**: 60 seconds (runner registration time)

**Unhealthy Status**: If the runner shows `(unhealthy)`:
1. Check logs: `docker compose logs github-runner`
2. Common causes:
   - Token expired or invalid
   - Network connectivity issues
   - GitHub API rate limiting
   - Runner process crashed

## Testcontainers Integration

The runner is configured to run integration tests that use Testcontainers (e.g., PostgreSQL containers for database tests).

### How It Works

1. **Docker-outside-of-Docker (DooD)**: Runner container shares the host Docker daemon
2. **Sibling Containers**: Testcontainers spins up PostgreSQL as a sibling to the runner (not nested)
3. **Host Override**: `TESTCONTAINERS_HOST_OVERRIDE=host-gateway` allows runner to reach sibling containers
4. **Ryuk Cleanup**: Testcontainers' Ryuk container automatically cleans up test containers after runs

### Testcontainers Network Diagram

```
┌─────────────────────────────────────────────────────────┐
│ Docker Host                                             │
│                                                         │
│  ┌──────────────────┐      ┌─────────────────────────┐ │
│  │ github-runner    │      │ testcontainers-postgres │ │
│  │ (CI workflows)   │─────▶│ (ephemeral)             │ │
│  │                  │      │ TESTCONTAINERS_HOST_    │ │
│  │ /var/run/        │      │ OVERRIDE resolves here  │ │
│  │ docker.sock      │      └─────────────────────────┘ │
│  └────────┬─────────┘               ▲                  │
│           │                         │                  │
│           │ Socket Mount            │ Sibling          │
│           ▼                         │ Containers       │
│  ┌─────────────────────────────────┴──────────────┐   │
│  │ Docker Engine (/var/run/docker.sock)          │   │
│  │ - Manages all containers                       │   │
│  │ - Shared by runner and Testcontainers         │   │
│  └────────────────────────────────────────────────┘   │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Verifying Testcontainers

Run the integration tests locally to verify the setup:

```bash
# Start the runner
docker compose up -d github-runner

# Trigger a test workflow or run tests manually inside the runner
docker compose exec github-runner bash
# Inside runner container:
cd /tmp/runner/work/SeriesScraper/SeriesScraper
dotnet test --filter Category=Integration
exit
```

**Expected Behavior**:
1. Testcontainers downloads PostgreSQL image (first run only)
2. Ephemeral PostgreSQL container starts as sibling to runner
3. Tests execute against the test database
4. Ryuk cleans up PostgreSQL container after tests complete

## Common Operations

### Restart the Runner

```bash
# Restart just the runner service
docker compose restart github-runner

# Rebuild and restart (after updating environment variables)
docker compose up -d --build github-runner
```

### View Runner Logs

```bash
# Live tail
docker compose logs -f github-runner

# Last 100 lines
docker compose logs --tail=100 github-runner

# Export logs for debugging
docker compose logs github-runner > runner-debug.log
```

### Deregister the Runner

```bash
# Stop and remove the runner container
docker compose stop github-runner
docker compose rm -f github-runner

# Remove the runner from GitHub UI
# Navigate to: Settings → Actions → Runners → Click '...' → Remove
```

**Note**: The PAT-based registration persists across container restarts. Removing the container does NOT automatically deregister from GitHub.

### Update Runner Image

```bash
# Pull the latest image
docker pull myoung34/github-runner:2.320.0

# Recreate the runner with the new image
docker compose up -d --build github-runner
```

**Warning**: Always test runner updates on a non-main branch before deploying to production.

## Troubleshooting

### Runner Shows Offline in GitHub

**Symptoms**: Runner appears in GitHub but status is Offline (gray circle)

**Causes & Solutions**:
1. **Token Expired**: Regenerate PAT and update `.env`, then `docker compose restart github-runner`
2. **Wrong Repository URL**: Verify `GITHUB_REPO_URL` matches exactly (no trailing slash)
3. **Network Issues**: Check Docker network connectivity: `docker compose exec github-runner ping github.com`
4. **Rate Limiting**: GitHub API rate limit exceeded — wait 1 hour

### Workflows Not Running on Self-Hosted Runner

**Symptoms**: Workflows queue indefinitely or run on GitHub-hosted runners

**Causes & Solutions**:
1. **Label Mismatch**: Verify workflow has `runs-on: self-hosted` (not `runs-on: ubuntu-latest`)
2. **Runner Offline**: Check runner status in GitHub Settings → Actions → Runners
3. **Runner Busy**: Check `docker compose logs github-runner` for active jobs

### Tests Fail with "Cannot Connect to Docker Daemon"

**Symptoms**: Testcontainers throws `DockerException: Cannot connect to Docker daemon`

**Causes & Solutions**:
1. **Socket Not Mounted**: Verify `/var/run/docker.sock:/var/run/docker.sock` in `docker-compose.yml`
2. **Socket Permissions**: On Linux, ensure user is in `docker` group
3. **Windows/Mac**: Restart Docker Desktop

### Tests Fail with "Connection Refused to Testcontainers"

**Symptoms**: Tests cannot connect to Testcontainers PostgreSQL (e.g., `Connection refused on port 5432`)

**Causes & Solutions**:
1. **Host Override Incorrect**: On Linux, use `TESTCONTAINERS_HOST_OVERRIDE=host-gateway`. On Windows/Mac, use `host.docker.internal`
2. **Network Isolation**: Verify runner is on `seriescraper-network`: `docker network inspect seriescraper-network`
3. **Firewall Blocking**: Temporarily disable firewall to test

### Ryuk Container Fails to Start

**Symptoms**: Testcontainers logs show `Ryuk startup failed`

**Causes & Solutions**:
1. **Disk Space**: Ryuk image (~10MB) failed to pull — check available disk space
2. **Private Registry**: Override Ryuk image: `TESTCONTAINERS_RYUK_IMAGE=your-registry/ryuk:0.6.0`
3. **Disable Ryuk**: Set `TESTCONTAINERS_RYUK_DISABLED=true` (cleanup becomes manual)

## Maintenance

### Monthly Tasks

- [ ] Rotate `GITHUB_ACCESS_TOKEN` (expires every 90 days — rotate at 80 days)
- [ ] Update runner image to latest stable version
- [ ] Review runner logs for errors or warnings
- [ ] Verify no orphaned Testcontainers: `docker ps -a | grep testcontainers`

### After Docker Engine Updates

- [ ] Restart runner: `docker compose restart github-runner`
- [ ] Verify health check passes: `docker compose ps`
- [ ] Run a test workflow to confirm functionality

### Before Major Changes (e.g., Workflow Modifications)

- [ ] Create a test branch to validate changes
- [ ] Monitor runner logs during test runs
- [ ] Verify all tests pass before merging to main

## Performance Tuning

### Resource Limits (Future Enhancement)

Currently, the runner has no resource limits. To prevent resource exhaustion:

```yaml
github-runner:
  # ... existing configuration ...
  deploy:
    resources:
      limits:
        cpus: '2.0'
        memory: 4G
      reservations:
        cpus: '1.0'
        memory: 2G
```

**Trade-off**: Limits may slow down builds but prevent host DoS.

### Concurrent Jobs

By default, one runner handles one job at a time. To run multiple jobs concurrently:
- Deploy multiple runner containers with unique `RUNNER_NAME` values
- Alternative: Configure `RUNNER_CONCURRENCY` (not supported by `myoung34/github-runner`)

**Recommendation**: Start with 1 runner; scale only if queue times exceed 5 minutes.

## References

- [GitHub Actions Self-Hosted Runners](https://docs.github.com/en/actions/hosting-your-own-runners)
- [myoung34/github-runner Docker Image](https://github.com/myoung34/docker-github-actions-runner)
- [Testcontainers Documentation](https://www.testcontainers.org/)
- [Docker-outside-of-Docker Pattern](https://jpetazzo.github.io/2015/09/03/do-not-use-docker-in-docker-for-ci/)
