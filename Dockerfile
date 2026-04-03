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
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install curl for health checks + Playwright Chromium dependencies (#89)
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    libnss3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libpango-1.0-0 \
    libcairo2 \
    libasound2 \
    libatspi2.0-0 \
    libwayland-client0 \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# ─── Build Image ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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
    --no-restore

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

# Install Playwright Chromium browser for reCAPTCHA v3 authentication (#89)
# Uses the Playwright CLI from the published app to install browsers
RUN dotnet Microsoft.Playwright.dll install chromium

# Create non-root user for security (principle of least privilege)
RUN addgroup --gid 1001 appgroup \
    && adduser --uid 1001 --ingroup appgroup --disabled-password --gecos "" appuser \
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
