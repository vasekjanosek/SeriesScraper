# History Page Wireframe

**Issue**: #33  
**Route**: `/history`

## User Flow

1. User navigates to History page to view past scrape runs
2. Runs are listed in reverse chronological order (newest first)
3. User scans list to find specific run by date, forum, or status
4. User clicks on a run row to navigate to results page `/runs/{runId}/results`
5. User sees "Stale" badges on runs with outdated results (`is_current = false`)
6. Pagination appears when more than 50 runs exist

## Interactive Elements

- **Run row**: clickable, navigates to `/runs/{runId}/results`
- **Pagination controls**: Previous/Next buttons and page numbers
- **Filter note**: "Filtering will be added in a future version" (static text)

## States

- **Loading**: skeleton rows while data loads
- **Success with results**: runs displayed in table
- **Empty state**: no runs in history message
- **Paginated**: more than 50 runs, pagination controls visible

## Edge Cases

- Zero runs in history (new installation)
- Very long forum names (truncate with ellipsis)
- All runs marked as stale
- Maximum item count display (e.g., 999+ items)
- Retention policy: some old runs may be pruned based on global setting (#36)

## Future Features (Deferred)

- Filter by item name (explicitly deferred)
- Filter by date range (explicitly deferred)
- Bulk delete of history entries (out of scope)
