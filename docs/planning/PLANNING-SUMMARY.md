# Planner Agent — Quick Reference Summary

**Date**: 2026-04-02  
**Status**: Planning phase complete for remaining feature issues

---

## ✅ Issues Ready for Development (status:ready)

Apply these labels via GitHub:

### #12 — Forum section discovery and classification
- Labels: `status:ready`, `agent:developer`, `priority:high`, `dev-slot:1`
- All dependencies met: #3 ✓, #6 (research) ✓, #28 ✓
- Unblocks: #13, #16

### #23 — IMDB title matching engine ⚠️ CRITICAL PRIORITY
- Labels: `status:ready`, `agent:developer`, `priority:critical`, `dev-slot:2`
- All dependencies met: #21 ✓, #22 ✓, #4 ✓
- Blocks 3 other tasks: #16, #31, #19
- **Start this first**

### #17 — Forum session management and re-authentication
- Labels: `status:ready`, `agent:developer`, `priority:high`, `dev-slot:3`
- All dependencies met: #3 ✓, Serilog ✓
- Core scraping infrastructure

---

## 🔴 Blocked Issues

**Dependency blocks**:
- #13 → needs #12 + #34/#36
- #16 → needs #23
- #19 → needs #31 (circular)
- #31 → needs #23 + #19 (circular)
- #32 → needs #16
- #33 → needs #34/#36

**Human decision block**:
- #34/#36 → needs #9 → needs #49 human decision

---

## ⚠️ Action Items Required

### 1. Apply labels (Orchestrator or manual)
Mark #12, #23, #17 as `status:ready` + `agent:developer`

### 2. Resolve duplicate issues (PM)
Issues #34 and #36 are identical — close one as duplicate

### 3. Break circular dependency (PM)
Split #31 into:
- **#31a** — Results page WITHOUT watchlist (ready after #23 merges)
- **#31b** — Add watchlist badges (ready after #19 + #31a merge)

### 4. Human decision (needs @vasekjanosek)
Issue #49 blocks entire Settings pipeline (#9, #51, #34/#36, #33, Epic #35)

---

## 📋 Batch Execution Plan

**Batch 1** (start now):
- dev-slot:1 → #12 (forum discovery)
- dev-slot:2 → **#23** (IMDB matching) ← **PRIORITIZE**
- dev-slot:3 → #17 (session management)

**Batch 2** (after #23 merges):
- #31a (results without watchlist)
- #16 (multi-item search)
- #13 (if unblocked)

**Batch 3** (after #12, #16, #31a):
- #32 (progress page)
- #19 (watchlist)
- #13 (if still blocked, defer)

---

## 📄 Full Details

See: [docs/planning/PLANNING-REPORT-2026-04-02.md](PLANNING-REPORT-2026-04-02.md)
