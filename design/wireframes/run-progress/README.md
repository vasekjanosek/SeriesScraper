# Run Progress Page Wireframe

**Issue**: #32  
**Route**: `/runs/{runId}`

## User Flow

1. User starts a scrape run from the Search page
2. Browser navigates to `/runs/{runId}` showing progress in real-time
3. User monitors progress via per-item status list (Queued → Scraping → Matching → Done/Failed)
4. Real-time updates pushed via Blazor Server SignalR (no polling)
5. User can cancel active run or resume partial run
6. When run completes, user navigates to Results page

## Interactive Elements

- **Cancel button**: visible when run status = Running; sends cancellation signal, marks run as Partial
- **Resume button**: visible when run status = Partial; re-enqueues run from next pending item
- **View Results button**: visible when run status = Complete or Partial; navigates to `/runs/{runId}/results`
- **Global run status indicator**: persistent component visible on all pages (bottom status bar or header badge)

## States

- **Running**: progress updates pushed in real-time, cancel button enabled
- **Partial**: run was cancelled, resume button visible
- **Complete**: all items done, view results button visible
- **Failed**: error state with retry option

## Real-Time Updates

- Updates delivered via Blazor Server SignalR connection
- Background service pushes state changes to connected clients
- NO polling timers in UI component
- Global `IRunStatusService` singleton provides run status to app shell component

## Edge Cases

- SignalR connection lost: display reconnection message
- Very long error messages: truncate with expand button
- Cancellation during state transition: graceful handling
- Multiple concurrent clients viewing same run
