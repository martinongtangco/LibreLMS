# Implementation Plan: Spec Bookkeeping Cleanup

## Changes Required

### 1. Status lines (spec.md) — 25 replacements, 4 insertions

Replace the first `**Status**:` line with `Complete (merged <date>)`:

| Spec | Old status | Merged |
|------|-----------|--------|
| 001 | Draft | 2026-07-28 (initial slice) |
| 002 | Draft | 2026-07-28 |
| 003 | Draft | 2026-07-31 (3dad925) |
| 004 | Draft | 2026-07-31 (a4a6f63) |
| 005 | Draft | 2026-07-30 (75574e6) |
| 006 | Draft | 2026-07-30 (75574e6) |
| 007 | Draft | 2026-07-31 (01bef28) |
| 008 | Draft | 2026-07-31 (08e9ba8) |
| 009 | Draft | 2026-07-31 (1e572d5) |
| 013 | Draft | 2026-07-31 (f554ace, PR #5) |
| 014 | Implementing | 2026-07-31 (b0c6a5f) |
| 015 | Draft | 2026-08-03 (92fce80) |
| 016 | Draft | 2026-08-03 (9403764) |
| 017 | Draft | 2026-08-03 (75a215a) |
| 018 | Draft | 2026-08-03 (c145233) |
| 019 | Draft | 2026-08-05 (7979a6e, PR #6) |
| 020 | Draft | 2026-08-05 (3d4dca1) |
| 021 | Draft | 2026-08-05 (41d17a6) |
| 022 | Draft | 2026-08-07 (9ddc4b3) |
| 025 | Draft | 2026-08-12 (47f9a8d) |
| 026 | Draft | 2026-08-12 (0b3e059) |
| 027 | Draft | 2026-08-15 (d1092ce) |
| 029 | Draft | 2026-08-16 (f5aa5eb) |
| 030 | Ready for Planning | 2026-08-16 (b3a4398) |
| 032 | Draft | 2026-08-22 (6ae801b) |

Insert `**Status**: Complete (merged <date>)` after the H1 title (blank-line separated):
010 (2026-07-31), 011 (2026-07-31), 012 (2026-07-31), 024 (2026-08-10).

### 2. Task checkboxes (tasks.md)

Check off every open `- [ ]` → `- [x]` in: 008 (29), 018 (19), 025 (29), 026 (7).
All underlying work verified present on master (merge commits listed above).

## Verification

- `grep -rc "^\s*- \[ \]" specs/*/tasks.md | grep -v ':0$'` → no output
- `grep -L "\*\*Status\*\*: Complete" specs/*/spec.md` → no output
- `git diff --name-only` → only files under `specs/`
