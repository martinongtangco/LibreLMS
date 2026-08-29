# Tasks: Deterministic Dashboard Percent Format

**Input**: [plan.md](plan.md), [spec.md](spec.md)

## Phase 1: Setup

- [X] T00[1-5] Create branch `bug/043-fix-dashboard-percent-format` from `master` and confirm `git branch --show-current` reports it (Principle VIII)

## Phase 2: Fix

- [X] T00[1-5] In `src/Host/Pages/Admin/Dashboard/Index.cshtml.cs`, replace both `metrics.AverageCompletionRate.ToString("P1")` calls (super-user and OrgAdmin branches) with `metrics.AverageCompletionRate.ToString("0.#%")`; add a one-line comment at the first site explaining the format is custom (not `P1`) on purpose — standard `P*` formats are culture-dependent (bug-043)

## Phase 3: Verification (Principle XIII)

- [X] T00[1-5] Rebuild + restart the app (`rm -rf src/Host/obj src/Host/bin && dotnet build src/Host`, relaunch per quickstart pattern), show build output + "Now listening" + 200 from `/Courses`
- [X] T00[1-5] Run `npx playwright test tests/04-admin-dashboard.spec.ts` from `tests/Playwright.Tests` and capture passing output (the bug-039 guard is the pin)
- [X] T00[1-5] Run the FULL `npx playwright test` suite and capture passing output (confirms the block on spec 042 T023/T025 is cleared; note any transient flakes with re-run evidence)

## Phase 4: Merge

- [ ] T006 Merge `bug/043-fix-dashboard-percent-format` into `master`, then on `master` rebuild + restart + re-run the full `npx playwright test` (Principle XIII gate 3 — post-merge regression), and switch back to `master` clean (Principle XII)
