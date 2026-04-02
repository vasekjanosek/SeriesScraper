# SeriesScraper — Dockerfile
# Placeholder: DevOps agent will finalize this once the solution structure is defined
# by the Architect agent. The structure below assumes a standard .NET 8 Blazor app.
#
# Expected solution layout (to be confirmed by Architect):
#   src/SeriesScraper.Web/SeriesScraper.Web.csproj   ← Blazor frontend + host
#   src/SeriesScraper.Core/...                        ← Domain / business logic
#   src/SeriesScraper.Infrastructure/...              ← Data access, external APIs
#   SeriesScraper.sln

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# [DevOps agent: update COPY paths once solution structure is defined]
COPY ["SeriesScraper.sln", "./"]
COPY ["src/", "src/"]

RUN dotnet restore

RUN dotnet build --no-restore --configuration Release

FROM build AS publish
RUN dotnet publish \
    --no-build \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as non-root for security
RUN addgroup --system --gid 1001 appgroup \
    && adduser --system --uid 1001 --ingroup appgroup appuser
USER appuser

ENTRYPOINT ["dotnet", "SeriesScraper.Web.dll"]
