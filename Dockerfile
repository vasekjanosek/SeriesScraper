# ═══════════════════════════════════════════════════════════════════════════
# SeriesScraper — Multi-Stage Dockerfile for Clean Architecture
# ═══════════════════════════════════════════════════════════════════════════
# Solution structure (ADR-001):
#   SeriesScraper.sln
#   ├── src/
#   │   ├── SeriesScraper.Domain/              ← Entities, interfaces, value objects
#   │   ├── SeriesScraper.Application/         ← Use cases, DTOs, application services
#   │   ├── SeriesScraper.Infrastructure/      ← EF Core, repositories, HTTP clients
#   │   └── SeriesScraper.Web/                 ← Blazor Server, DI composition root
#   └── tests/
#       ├── SeriesScraper.Domain.Tests/
#       ├── SeriesScraper.Application.Tests/
#       ├── SeriesScraper.Infrastructure.Tests/
#       └── SeriesScraper.Web.Tests/
# ═══════════════════════════════════════════════════════════════════════════

# ─── Base Runtime Image ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Install curl for health checks (health check uses curl -f http://localhost:8080/healthz)
RUN apk add --no-cache curl

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# ─── Build Image ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy solution file and all project files for dependency caching
COPY ["SeriesScraper.sln", "./"]
COPY ["src/SeriesScraper.Domain/SeriesScraper.Domain.csproj", "src/SeriesScraper.Domain/"]
COPY ["src/SeriesScraper.Application/SeriesScraper.Application.csproj", "src/SeriesScraper.Application/"]
COPY ["src/SeriesScraper.Infrastructure/SeriesScraper.Infrastructure.csproj", "src/SeriesScraper.Infrastructure/"]
COPY ["src/SeriesScraper.Web/SeriesScraper.Web.csproj", "src/SeriesScraper.Web/"]
COPY ["tests/SeriesScraper.Domain.Tests/SeriesScraper.Domain.Tests.csproj", "tests/SeriesScraper.Domain.Tests/"]
COPY ["tests/SeriesScraper.Application.Tests/SeriesScraper.Application.Tests.csproj", "tests/SeriesScraper.Application.Tests/"]
COPY ["tests/SeriesScraper.Infrastructure.Tests/SeriesScraper.Infrastructure.Tests.csproj", "tests/SeriesScraper.Infrastructure.Tests/"]
COPY ["tests/SeriesScraper.Web.Tests/SeriesScraper.Web.Tests.csproj", "tests/SeriesScraper.Web.Tests/"]

# Restore dependencies (cached layer unless .csproj files change)
RUN dotnet restore "SeriesScraper.sln"

# Copy all source code
COPY . .

# Build all projects in Release configuration
WORKDIR "/src/src/SeriesScraper.Web"
RUN dotnet build "SeriesScraper.Web.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/build

# ─── Publish Image ─────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "SeriesScraper.Web.csproj" \
    --configuration Release \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

# ─── Final Runtime Image ───────────────────────────────────────────────────
FROM base AS final
WORKDIR /app

# Copy published application from publish stage
COPY --from=publish /app/publish .

# Create non-root user for security (principle of least privilege)
RUN addgroup -g 1001 -S appgroup \
    && adduser -u 1001 -S appuser -G appgroup \
    && chown -R appuser:appgroup /app

# Create directories for persistent data with correct permissions
RUN mkdir -p /app/data/imdb /app/logs \
    && chown -R appuser:appgroup /app/data /app/logs

USER appuser

# Health check endpoint — verifies DB connectivity + app responsiveness
# Configured in docker-compose.yml: curl -f http://localhost:8080/healthz
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "SeriesScraper.Web.dll"]
