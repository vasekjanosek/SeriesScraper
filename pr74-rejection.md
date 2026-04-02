## Tester Rejection (test-cycle: 1)

### Test Results
- **Build**: ✓ Passed
- **Tests**: ✓ 985 passed, 0 failed
- **Verdict**: ❌ **REJECTED**

### Coverage Analysis
Coverage checked on all new code:

**Classes Added in PR #74:**
1. WatchlistService: 100% ✓
2. WatchlistItem: 100% ✓
3. **WatchlistRepository: 0% ❌ FAIL**

### Coverage Gap Details

**WatchlistRepository** (`src/SeriesScraper.Infrastructure/Repositories/WatchlistRepository.cs`)
- **Coverage**: 0% (0 of 100 lines covered)
- **Required**: ≥90%
- **Gap**: 90 percentage points below threshold

**Uncovered Code (ALL lines uncovered):**
- Line 15-20: `AddAsync()` - Add + SaveChangesAsync
- Line 22-27: `GetByIdAsync()` - FirstOrDefaultAsync with Include
- Line 29-41: `GetAllAsync()` - conditional Where + OrderByDescending + ToListAsync
- Line 43-47: `ExistsByMediaTitleIdAsync()` - AnyAsync check
- Line 49-56: `RemoveAsync()` - FindAsync + conditional Remove + SaveChangesAsync
- Line 58-62: `UpdateAsync()` - Update + SaveChangesAsync
- Line 64-93: `GetItemsWithNewMatchesAsync()` - complex method with:
  - Active items query with Include
  - Loop with conditional query per item
  - LastMatchedAt timestamp update
  - Conditional SaveChangesAsync

### Required Fixes

1. Create `WatchlistRepositoryTests.cs` in `tests/SeriesScraper.Infrastructure.Tests/Repositories/`
2. Test all methods with TestContainers + PostgreSQL + seeded data
3. Coverage scenarios:
   - **AddAsync**: verify item added + returned with generated ID
   - **GetByIdAsync**: existing item (verify MediaTitle included), non-existent item
   - **GetAllAsync**: 
     - activeOnly=true: only active items returned
     - activeOnly=false: all items returned
     - Verify OrderByDescending(AddedAt)
     - Empty collection
   - **ExistsByMediaTitleIdAsync**: exists, doesn't exist
   - **RemoveAsync**: existing item removed, non-existent item (no error)
   - **UpdateAsync**: item updated successfully
   - **GetItemsWithNewMatchesAsync**:
     - No active items
     - Active items with no new matches
     - Active items with new matches (verify LastMatchedAt updated)
     - Items with/without LastMatchedAt set
     - Multiple items with mixed new match counts
   - Edge cases: cancellation token, null MediaTitleId
4. Achieve ≥90% line/branch coverage on WatchlistRepository

### Commands to Reproduce
```powershell
git checkout feature/issue-19-watchlist
dotnet build
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport"
# Check WatchlistRepository coverage in Summary.txt
```

Returning to Developer for test implementation.

Labels: status:in-testing → status:in-progress, agent:tester → agent:developer
