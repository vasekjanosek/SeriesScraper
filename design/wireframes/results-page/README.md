# Results Page Wireframe

**Issue**: #31  
**Route**: `/runs/{runId}/results`

## User Flow

1. User navigates to Results page after completing a scrape run (or from History page)
2. Page loads results grouped by post, ranked by quality
3. User scans results to find high-quality matches:
   - Watchlist matches are highlighted with colored border
   - Matched posts show IMDB title + year
   - Unmatched posts appear greyed out at the bottom
4. User clicks on post title or links to navigate to download sources
5. Empty state: if no results, user sees message with link back to Search page

## Interactive Elements

- **Post title**: clickable link to original forum post
- **IMDB title**: clickable link to IMDB page
- **Links**: download links (validated schemes only)
- **"Back to Search" button**: returns to search page (empty state only)

## States

- **Loading**: skeleton cards while data loads
- **Success with results**: post groups displayed as cards
- **Empty state**: no results message with call-to-action
- **Error state**: error message if data fetch fails

## Edge Cases

- Zero results for run
- Very long post titles (truncate with ellipsis)
- Missing IMDB match data
- Invalid URL schemes (replaced with #)
- XSS prevention: all scraped content sanitized
