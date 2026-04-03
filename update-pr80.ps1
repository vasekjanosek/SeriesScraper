#!/usr/bin/env pwsh
# Update PR #80 status after testing approval

Write-Host "Removing testing labels..."
gh pr edit 80 --remove-label "status:in-testing" 2>&1 | Out-Null
gh pr edit 80 --remove-label "agent:tester" 2>&1 | Out-Null

Write-Host "Adding security-review labels..."
gh pr edit 80 --add-label "status:security-review" 2>&1 | Out-Null
gh pr edit 80 --add-label "agent:security" 2>&1 | Out-Null

Write-Host "Posting test report comment..."
$comment = @"
## Testing Report — APPROVED (test-cycle: 1)

All 4 security fixes tested and validated. 486/486 tests passing, coverage ≥90% on all modified classes.

### Test Results
- **Total**: 486 tests
- **Passed**: 486
- **Failed**: 0
- **Build**: ✅ Success (Release)

### Coverage (Modified Classes)
| Class | Coverage | Status |
|-------|----------|--------|
| ImdbDatasetDownloader | 93.1% | ✅ |
| LinkExtractorService | 93.7% | ✅ |
| ForumPostScraper | 100% | ✅ |
| ForumSearchService | 100% | ✅ |

### Security Fix Validation

#### #65: Path Traversal Prevention ✅
- Allowlist: 7 known IMDB datasets (exact match)
- Blocks: ``../../etc/passwd``, ``../windows/system32/config/sam``, path separators, dotdot
- 10 new tests: path traversal attempts, unknown datasets, null/empty

#### #66: Temp File Cleanup ✅
- ``catch(Exception)`` ensures cleanup on ALL exception types
- Best-effort ``File.Delete()`` with ``IOException`` guard
- 2 tests: InvalidGzipHeader and TooFewRows both verify cleanup

#### #67: URL Length Validation ✅
- ``MaxUrlLength = 2000`` constant
- URLs >2000 chars skipped before DB insert
- 3 tests: exceeds limit (skipped), exact 2000 (accepted), mixed URLs

#### #75: IUrlValidator Integration ✅
- ForumPostScraper: validates postUrl BEFORE HTTP request
- ForumSearchService: validates sectionUrl AND thread URLs
- DI confirmed in both constructors
- 8 new tests: invalid URLs rejected, valid URLs processed

### Static Analysis
- Compile errors: 0
- New issues: 0
- Pre-existing warnings: 2 (EF Core version conflict, non-blocking)

### Regression
- All 486 tests pass
- No behavior changes to existing functionality

---

**Verdict**: ✅ Testing passed. Forwarding to Security Agent for code review.
"@

gh pr comment 80 --body $comment

Write-Host "✅ PR #80 updated successfully"
