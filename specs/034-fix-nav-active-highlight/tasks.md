# Tasks: Fix Admin "Courses" Nav Highlight

## Story 1 — Admin Courses page highlights "Courses" (P1)

- [ ] T1.1 Replace substring matching in the `_Layout.cshtml` active-link IIFE with exact / section-prefix matching; longest match wins; remove the dead lowercase branch (FR-001, FR-002, FR-007)

## Story 2 — Highlight persists across pagination (P1)

- [ ] T2.1 Add `17-nav-active-highlight.spec.ts`: SuperUser login; page-1 highlight assertions on `/Admin/Courses/Index` (SC-001)
- [ ] T2.2 Filler-data setup (11 marker courses via admin API, cleanup in afterAll) + "Next →" page-2 highlight assertions (SC-002)
- [ ] T2.3 Pathname-level pagination checks for Enrollments/Learners admin lists (FR-004)

## Story 3 — Subpages keep their section highlighted (P2)

- [ ] T3.1 Subpage assertions: `/Courses/Detail` → Browse Courses; `/Admin/Courses/Edit` → admin Courses (SC-003)
- [ ] T3.2 No-highlight assertions: `/` (authenticated) and `/Account/Login` (logged out) (FR-006)

## Verification (Principle XIII)

- [ ] T4.1 `dotnet build` passes (show output)
- [ ] T4.2 App restarted, `Now listening` + HTTP 200 (show output)
- [ ] T4.3 Playwright 17-nav-active-highlight passes (show output)
- [ ] T4.4 Regression: Playwright specs 02/04/05/07/10/16 pass (show output)
- [ ] T4.5 Merge to master, rebuild, restart, re-run Playwright (show output); return to master
