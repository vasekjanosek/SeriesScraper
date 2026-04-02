# Health Check Implementation — Required for Issue #38

## Overview

The Docker Compose configuration requires an ASP.NET Core health check endpoint at `/healthz` that verifies database connectivity. This document specifies the implementation requirements.

## Endpoint Specification

- **URL**: `/healthz`
- **Method**: GET
- **Response**: 200 OK (healthy) | 503 Service Unavailable (unhealthy)
- **Purpose**: Verify application responsiveness AND PostgreSQL database connectivity

## Implementation Location

Per ADR-001 Clean Architecture structure:

```
src/SeriesScraper.Web/
├── Program.cs                              ← Register health checks in DI
└── HealthChecks/
    └── DatabaseHealthCheck.cs              ← Custom IHealthCheck implementation
```

## Code Template

### 1. DatabaseHealthCheck.cs

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SeriesScraper.Web.HealthChecks;

/// <summary>
/// Health check that verifies PostgreSQL database connectivity.
/// Used by Docker Compose healthcheck: curl -f http://localhost:8080/healthz
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        IConfiguration configuration,
        ILogger<DatabaseHealthCheck> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            // Verify connection with simple query
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("PostgreSQL connection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed: {Message}", ex.Message);
            return HealthCheckResult.Unhealthy(
                "PostgreSQL connection failed",
                exception: ex);
        }
    }
}
```

### 2. Program.cs Registration

```csharp
// Add health checks to DI container
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "postgresql" });

// ... after app.Build() ...

// Map health check endpoint
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});
```

## Docker Compose Integration

The health check is configured in `docker-compose.yml`:

```yaml
app:
  healthcheck:
    test: ["CMD-SHELL", "curl -f http://localhost:8080/healthz || exit 1"]
    interval: 30s
    timeout: 10s
    retries: 3
    start_period: 40s
```

### Health Check Parameters

- **interval**: Check every 30 seconds after the container starts
- **timeout**: Wait up to 10 seconds for a response
- **retries**: Mark unhealthy after 3 consecutive failures
- **start_period**: Grace period of 40 seconds for app startup (EF migrations, etc.)

## Testing

### Manual Testing

1. Start services: `docker compose up`
2. Wait for app health check to pass
3. Test endpoint:
   ```bash
   curl -v http://localhost:8080/healthz
   ```
4. Expected response (200 OK):
   ```json
   {
     "status": "Healthy",
     "checks": [
       {
         "name": "database",
         "status": "Healthy",
         "description": "PostgreSQL connection successful",
         "duration": 15.3
       }
     ],
     "totalDuration": 15.3
   }
   ```

### Docker Health Check Status

```bash
# View health check status
docker compose ps

# Expected output:
# NAME                 STATUS                     HEALTH
# seriescraper-app-1   Up 2 minutes (healthy)
# seriescraper-db-1    Up 2 minutes (healthy)
```

### Failure Scenario Testing

1. Stop PostgreSQL: `docker compose stop db`
2. Wait 30 seconds for health check to run
3. Check status: `docker compose ps`
4. Expected: `seriescraper-app-1` shows `(unhealthy)`
5. Test endpoint:
   ```bash
   curl -v http://localhost:8080/healthz
   ```
6. Expected response (503 Service Unavailable):
   ```json
   {
     "status": "Unhealthy",
     "checks": [
       {
         "name": "database",
         "status": "Unhealthy",
         "description": "PostgreSQL connection failed",
         "duration": 2015.7
       }
     ],
     "totalDuration": 2015.7
   }
   ```

## Dependencies

- NuGet package: `Microsoft.Extensions.Diagnostics.HealthChecks`
- NuGet package: `Npgsql` (already required for EF Core)
- NuGet package: `Microsoft.AspNetCore.Diagnostics.HealthChecks` (in aspnet:8.0 runtime)

## Related Issues

- #38: Docker Compose setup (this file supports AC #4)
- #39: CI/CD pipeline (health check must pass before marking build successful)
- ADR-001: System Architecture (health check is part of Web project)

## Security Considerations

- Health check endpoint is unauthenticated — by design for Docker health checks
- Endpoint is only accessible within the Docker network and localhost (per docker-compose.yml port binding)
- Does not expose sensitive information (connection strings, credentials)
- Exception messages are logged but sanitized in the response

## Implementation Priority

**CRITICAL** — The health check must be implemented before the Docker Compose setup can be considered complete per AC #4. The `app` service depends on the `/healthz` endpoint existing and responding correctly.

## Developer Agent TODO

When implementing SeriesScraper.Web project structure:
1. Create `src/SeriesScraper.Web/HealthChecks/DatabaseHealthCheck.cs`
2. Register health checks in `Program.cs` as shown above
3. Test with `docker compose up` and verify health status
4. Ensure health check passes before opening PRs for dependent issues (#39, #41)
