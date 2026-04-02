# Health Check Endpoint Implementation

## Overview

The Docker Compose configuration requires a health check endpoint at `/healthz` that verifies:
1. Application is running and responding to HTTP requests
2. Database connection is healthy and accessible
3. Critical dependencies are available

## Implementation Requirements

### Endpoint Specification

**URL**: `/healthz`  
**Method**: `GET`  
**Expected Response**: HTTP 200 OK when healthy, HTTP 503 Service Unavailable when unhealthy

### Health Check Logic

The endpoint must verify:

1. **Database Connectivity**
   - Attempt to execute a simple query (e.g., `SELECT 1`)
   - Verify connection pool is not exhausted
   - Check that EF Core context can be created

2. **Response Format** (JSON)
   ```json
   {
     "status": "Healthy" | "Degraded" | "Unhealthy",
     "checks": {
       "database": "Healthy" | "Unhealthy",
       "timestamp": "2026-04-02T10:30:00Z"
     }
   }
   ```

### Implementation Approach (ASP.NET Core)

#### Option 1: Using Built-in Health Checks Middleware

In `Program.cs` or `Startup.cs`:

```csharp
// Add health checks services
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "database",
        timeout: TimeSpan.FromSeconds(5));

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
                duration = e.Value.Duration.ToString()
            }),
            timestamp = DateTime.UtcNow
        });
        await context.Response.WriteAsync(result);
    }
});
```

#### Option 2: Custom Health Check Class

Create `SeriesScraper.Web/HealthChecks/DatabaseHealthCheck.cs`:

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Web.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        ApplicationDbContext dbContext,
        ILogger<DatabaseHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Attempt simple database query
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            
            if (!canConnect)
            {
                _logger.LogWarning("Database health check failed: cannot connect");
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            return HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check threw exception");
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }
}
```

### Required NuGet Packages

```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="8.0.*" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="8.0.*" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.*" />
```

## Docker Configuration

The health check is already configured in `docker-compose.yml`:

```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -f http://localhost:8080/healthz || exit 1"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 60s
```

And in `Dockerfile`:

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1
```

## Testing

### Manual Testing

```bash
# Start the application
docker compose up -d

# Wait for services to start
sleep 60

# Check health status
curl -v http://localhost:8080/healthz

# Expected response: HTTP 200 with JSON body
```

### Automated Testing

Create integration test in `SeriesScraper.Web.Tests/HealthCheckTests.cs`:

```csharp
[Fact]
public async Task HealthCheck_WithHealthyDatabase_ReturnsHealthy()
{
    // Arrange
    var response = await _client.GetAsync("/healthz");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var content = await response.Content.ReadAsStringAsync();
    content.Should().Contain("\"status\":\"Healthy\"");
    content.Should().Contain("\"database\"");
}
```

## Security Considerations

- Health check endpoint exposes service status information
- Consider adding authentication if exposing to untrusted networks
- Do NOT expose sensitive configuration or connection strings in health check response
- Rate limiting may be appropriate to prevent abuse

## References

- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [ASP.NET Core HealthChecks GitHub](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
- Issue #38: Docker Compose setup
- ADR-001: System Architecture (if available)
