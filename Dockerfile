# ═════════════════════════════════════════════════════════════════════════════
# SeriesScraper — Multi-Stage Dockerfile for .NET 8 Blazor Application
# ═════════════════════════════════════════════════════════════════════════════
#
# Build strategy: Multi-stage build for minimal final image size
# Base image: Alpine Linux for security and size optimization
# Security: Non-root user execution (appuser:1001)
# Health check: Endpoint at /healthz for Docker health monitoring
#
# Expected solution structure (Clean Architecture):
#   SeriesScraper.sln
#   src/
#     SeriesScraper.Web/              ← Blazor Server frontend + host (entry point)
#     SeriesScraper.Application/      ← Application services, use cases
#     SeriesScraper.Domain/           ← Domain entities, interfaces
#     SeriesScraper.Infrastructure/   ← Data access, external APIs, EF Core
#   tests/
#     SeriesScraper.Web.Tests/
#     SeriesScraper.Application.Tests/
#     SeriesScraper.Domain.Tests/
#     SeriesScraper.Infrastructure.Tests/
#
# ═════════════════════════════════════════════════════════════════════════════

# ─── Stage 1: Base Runtime ───────────────────────────────────────────────────
# Alpine-based ASP.NET runtime with curl for health checks
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Install curl for Docker health check endpoint
RUN apk add --no-cache curl

# Set explicit URL binding for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080

# ─── Stage 2: Build ──────────────────────────────────────────────────────────
# SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy solution file and project files for dependency restoration
# This layer is cached unless project files change
COPY ["SeriesScraper.sln", "./"]
COPY ["src/SeriesScraper.Web/SeriesScraper.Web.csproj", "src/SeriesScraper.Web/"]
COPY ["src/SeriesScraper.Application/SeriesScraper.Application.csproj", "src/SeriesScraper.Application/"]
COPY ["src/SeriesScraper.Domain/SeriesScraper.Domain.csproj", "src/SeriesScraper.Domain/"]
COPY ["src/SeriesScraper.Infrastructure/SeriesScraper.Infrastructure.csproj", "src/SeriesScraper.Infrastructure/"]

# Restore dependencies (cached layer if project files unchanged)
RUN dotnet restore "SeriesScraper.sln"

# Copy remaining source code
COPY ["src/", "src/"]

# Build in Release configuration
RUN dotnet build "SeriesScraper.sln" \
    --configuration Release \
    --no-restore

# ─── Stage 3: Publish ────────────────────────────────────────────────────────
# Publish the application to /app/publish
FROM build AS publish
RUN dotnet publish "src/SeriesScraper.Web/SeriesScraper.Web.csproj" \
    --configuration Release \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

# ─── Stage 4: Final Runtime ──────────────────────────────────────────────────
# Minimal runtime image with published application
FROM base AS final
WORKDIR /app

# Copy published application from publish stage
COPY --from=publish /app/publish .

# Create non-root user and group for security
# UID/GID 1001 to avoid conflicts with common system users
RUN addgroup -g 1001 -S appgroup \
    && adduser -u 1001 -S appuser -G appgroup \
    && chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Health check: Verify application is responding and DB is reachable
# The /healthz endpoint should verify:
# - Application is running
# - Database connection is healthy
# - Critical dependencies are available
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

# Entry point: Start the Blazor application
ENTRYPOINT ["dotnet", "SeriesScraper.Web.dll"]
