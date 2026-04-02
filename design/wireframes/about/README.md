# About Page Wireframe

**Issue**: #34 (IMDB dataset status and app info)  
**Route**: `/about`

## User Flow

1. User navigates to About page (from footer link or main nav)
2. Page displays application information:
   - App version (from AssemblyInformationalVersion)
   - IMDB dataset import date and status
   - PostgreSQL connection status
   - License and attribution information
3. User views system health indicators
4. User can navigate to Settings to configure IMDB refresh

## Interactive Elements

- **Check Connection button**: tests PostgreSQL reachability (simple ping, no raw SQL)
- **Settings link**: navigates to `/settings` for configuration
- **GitHub repository link**: opens project repository (if public or accessible)

## States

- **Default**: all information displayed, connection status shown
- **Connection Test Running**: button disabled, loading indicator
- **Connection Success**: green checkmark displayed
- **Connection Failed**: red X with error hint (not full error details)

## Edge Cases

- IMDB dataset never imported (first run before initial sync)
- PostgreSQL connection unavailable
- Version string missing or malformed
- Very old IMDB import date (e.g., 90+ days ago)

## Data Sources

- **App version**: `AssemblyInformationalVersion` or equivalent
- **IMDB import date**: from `DataSourceImportRuns` table (dependency: #22)
- **PostgreSQL status**: simple reachability check (e.g., `SELECT 1`)
