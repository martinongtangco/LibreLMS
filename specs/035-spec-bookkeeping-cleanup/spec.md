# Bug Fix Specification: Spec Bookkeeping — Stale Statuses & Task Checkboxes

**Feature Branch**: `bug/035-spec-bookkeeping-cleanup`

**Created**: 2026-08-23

**Status**: Complete (merged 2026-08-23)

**Input**: Audit of `specs/` (2026-08-23) found that all 34 prior specs (001–034) are
implemented and merged to `master`, but their artifacts were never updated:

1. **Stale status lines** — 26 spec.md files still say `Draft` (or `Implementing` /
   `Ready for Planning`), and 4 lightweight specs (010, 011, 012, 024) have no
   `**Status**` field at all, even though their work is on `master`.
2. **Stale task checkboxes** — tasks.md for 008 (29 open), 018 (19 open), 025 (29 open),
   and 026 (7 open) were never checked off, although the corresponding work shipped
   (verified: merge commits on master cover every task).
3. **Branch hygiene** (already performed as git housekeeping, recorded here): all 33
   local `bug/` + `story/` branches were fully merged or verified stale duplicates
   (013/019 landed via PRs #5/#6; 031-t001 superseded; 001/008 remote tips are older
   history with no unique content beyond a dead `_CourseDetail.cshtml` partial) and
   were deleted. Only `master` remains locally.

## Root Cause

Implementation sessions merged their branches but never ran the final
"mark complete" bookkeeping step (the pattern later adopted for specs 028/031/033/034:
`docs(XXX): mark complete` on master after merge).

## Fix (documentation only — no code changes)

1. For every completed spec 001–032 (excluding already-complete 028/031/033/034):
   set `**Status**: Complete (merged YYYY-MM-DD)` in spec.md, using the actual merge
   commit date on `master`. Add a status line where missing (010/011/012/024).
2. Check off all tasks in tasks.md for 008, 018, 025, 026 (work verified on master).
3. Mark spec 035 itself complete via the post-merge docs commit on master.

## Verification

- `grep -L "Complete" specs/*/spec.md` → only spec 035's pre-mark state (or none).
- `grep -rc "^\s*- \[ \]" specs/*/tasks.md` → zero open checkboxes across all specs.
- No source files changed: `git diff --stat` shows only `specs/**/*.md`.
