# Settings Page Wireframe

**Issue**: #36  
**Route**: `/settings`

## User Flow

1. User navigates to Settings page
2. Page loads with three sub-navigation tabs: Global, Link Types, Forums
3. Global tab is active by default
4. User modifies global settings (intervals, thresholds, retention)
5. User clicks Save button to persist changes
6. Confirmation message appears after successful save
7. User can switch to Link Types or Forums tabs for those configurations

## Interactive Elements

- **Tab navigation**: switches between Global, Link Types, Forums without full page reload
- **Save button**: persists changes to database, shows confirmation message
- **Reset button**: reverts unsaved changes to last saved state
- **Numeric inputs**: validated for positive integers only
- **IMDB refresh interval**: dropdown or number input (hours)
- **Forum structure refresh interval**: dropdown or number input (hours)
- **Max concurrent threads**: number input (1-10 range recommended)
- **Quality pattern pruning threshold**: number input (hit count floor)
- **Result retention days**: number input (0 = retain all)

## States

- **Default**: form with current settings loaded
- **Modified**: Save button enabled when changes detected
- **Saving**: Save button disabled, loading indicator
- **Success**: confirmation message displayed
- **Validation error**: inline error messages for invalid inputs

## Sub-Tabs

1. **Global tab**: IMDB refresh interval, forum refresh interval, max threads, quality pruning threshold, retention days
2. **Link Types tab**: renders link type registry management UI from #28 (within tab panel)
3. **Forums tab**: renders forum CRUD UI from #9 (within tab panel)

## Edge Cases

- Invalid numeric input (non-integer, negative, or zero where not allowed)
- Very large retention days value (e.g., 9999 days)
- User navigates away with unsaved changes (confirmation prompt)
- First launch: all defaults seeded via EF Core migration
- Settings table empty: defaults loaded from seeded data
