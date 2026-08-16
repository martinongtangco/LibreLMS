# Tasks: Clickable Brand Link to Home

**Input**: Design documents from `/specs/029-clickable-home-brand/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — the feature spec requires E2E proof of the change (Constitution Principle XIII: "If no relevant test exists, the agent MUST write one before claiming completion"), and success criteria SC-001…SC-005 are only verifiable via Playwright against the running app (spec 022 convention).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

All paths are relative to the repository root (`/workspace`).

---

## Phase 1: Setup (Branch Creation)

**Purpose**: Create the implementation branch from master.

- [X] T001 Create branch `story/029-clickable-home-brand` from `master` and check it out

---

## Phase 2: User Story 1 - Escape the Login Page (Priority: P1) 🎯 MVP

**Goal**: The "Libre LMS" navbar brand becomes a link to Home (site root → Browse Courses) on every page, so a signed-out user on the Login page is no longer stuck — one click on the brand reaches Browse Courses.

**Independent Test**: In a fresh (signed-out) browser session, go to `/Account/Login`, click the "Libre LMS" brand in the navbar, and verify Browse Courses loads — one click, no prompts.

### Tests for User Story 1 ⚠️

> **NOTE: Write this test FIRST, ensure it FAILS before implementation**

- [X] T002 [P] [US1] Create `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` with test "signed-out user on Login page: brand is a link to Home" — navigate to `/Account/Login`, assert the `.brand` element is an `<a>` targeting the site root, click it, expect URL `/Courses` with the course listing visible and no sign-in prompt (maps to US1 acceptance scenarios 1–3, SC-001)

### Implementation for User Story 1

- [X] T003 [US1] In `src/Host/Pages/Shared/_Layout.cshtml`, replace `<span class="brand">Libre LMS</span>` with `<a href="/" class="brand">Libre LMS</a>` (single edit in the shared navbar — outside the authenticated/anonymous split, so it covers both states)
- [X] T004 [US1] In `src/Host/wwwroot/css/site.css`, add `text-decoration: none;` to the existing `.navbar .brand` rule (anchors would otherwise show the UA underline)
- [X] T005 [US1] In `src/Host/wwwroot/css/site.css`, add `.navbar .brand:hover { color: #ffffff; }` near the existing nav-link hover rules — hover affordance consistent with `.navbar .nav-links a.nav-link:hover` (FR-004)
- [X] T006 [US1] Rebuild and restart the Host app (`./scripts/restart-app.sh --background` from repo root; kill stale `bin/Debug/net10.0/Host` + `dotnet run` processes first and confirm ports 5000/7095 are free — Razor views do not hot-reload), then verify the Login page renders the brand as a link

**Checkpoint**: T002 passes. A signed-out user on the Login page reaches Browse Courses in exactly one brand click (SC-001, FR-001/003/004/008). User Story 1 is fully functional and independently testable.

---

## Phase 3: User Story 2 - One-Click Return to Home from Any Page (Priority: P1)

**Goal**: The brand returns signed-in users to Home from any section (learner and admin), with no role awareness — it never targets the admin Dashboard.

**Independent Test**: Sign in as the learner test user, open My Courses, click the brand → Browse Courses. Sign in as the org-admin test user with the Admin role view active, open the admin Dashboard, click the brand → Browse Courses (not the Dashboard).

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T007 [P] [US2] Add test "signed-in learner on My Courses: brand click lands on Browse Courses" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (sign in via `utils/testUsers` learner credentials, go to `/MyCourses/Index`, click `.brand`, expect `/Courses`; account name still visible — signed-in state preserved)
- [X] T008 [P] [US2] Add test "signed-in admin (Admin role view) on admin Dashboard: brand click lands on Browse Courses, NOT the admin Dashboard" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (sign in as org admin, set Admin role view, go to `/Admin/Dashboard/Index`, click `.brand`, expect `/Courses` and assert the admin Dashboard is not shown) (maps to US2 acceptance scenarios 1–3, FR-006)

### Implementation for User Story 2

- [X] T009 [US2] Verify no additional markup is required for signed-in users (the brand lives in the shared navbar outside the auth split — confirm T003's edit renders in both branches) and that no inline JS in `src/Host/Pages/Shared/_Layout.cshtml` (active-link, role switcher, hamburger, dropdown handlers) intercepts `.brand` clicks; confirm T007/T008 pass

**Checkpoint**: T007 and T008 pass. Brand click from learner and admin pages lands on Browse Courses with signed-in state preserved (FR-001, FR-006, SC-005).

---

## Phase 4: User Story 3 - Home Is Browse Courses by Default (Priority: P2)

**Goal**: Pin "Home = Browse Courses" as a tested requirement so it cannot regress (SC-003).

**Independent Test**: Open the site root `/` in a fresh session → Browse Courses shown (no sign-in wall). Sign in and open `/` again → same page.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T010 [P] [US3] Add test "anonymous visitor: root URL shows Browse Courses" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (navigate to `/`, expect final URL `/Courses` and the course listing visible)
- [X] T011 [P] [US3] Add test "signed-in user: root URL shows Browse Courses" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (sign in as learner, navigate to `/`, expect `/Courses` with the listing)

### Implementation for User Story 3

- [X] T012 [US3] Verify the root redirect already exists in `src/Host/Program.cs` (`MapGet("/", () => Results.Redirect("/Courses"))`) — verification only, no edit expected; confirm T010/T011 pass unchanged

**Checkpoint**: T010 and T011 pass with zero code changes (SC-003, US3 acceptance scenarios 1–2).

---

## Phase 5: Polish & Cross-Cutting Concerns (Edge Cases + Verification Gates)

**Purpose**: Cover the spec's edge cases and satisfy Constitution Principle XIII (compiles, E2E green, post-merge re-run) and Principle XII (return to master).

- [X] T013 [P] [US1] Add test "brand on Home is idempotent" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (on Browse Courses, click `.brand` — still on Browse Courses, clean load, no error/loop)
- [X] T014 [P] [US1] Add test "mobile 375px: brand visible and clickable on Login page (signed out)" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (375px viewport, brand visible outside the hamburger, click → `/Courses`) (FR-005)
- [X] T015 [P] [US2] Add test "access-denied login variant: brand present and navigates to Home" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (signed-in learner navigates to `/Admin/Dashboard/Index`, access-denied variant renders, click `.brand` → `/Courses`)
- [X] T016 [P] [US1] Add test "mobile: hamburger open, brand click resets nav state" to `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts` (375px, signed in, open hamburger, click `.brand` — lands on Browse Courses and the hamburger menu is closed) (FR-007)
- [X] T017 Run the full Playwright suite (`cd tests/Playwright.Tests && npx playwright test`) against the restarted app — all specs pass, no regressions (SC-005, Principle XIII gate 2)
- [X] T018 Run `dotnet test /workspace/tests/ArchitectureTests` — module boundary gate passes (Principle III)
- [X] T019 Run the manual quickstart.md scenarios (desktop 1280px + mobile 375px) — all 8 scenarios pass
- [X] T020 Commit all changes on `story/029-clickable-home-brand`, merge directly to `master` per user instruction, check out `master` again (Principle XII), then rebuild + restart and re-run the full Playwright suite on the merged code (Principle XIII gate 3 — post-merge regression)

**Checkpoint**: All edge-case tests pass; full E2E suite and ArchitectureTests green on the branch AND after the merge to master.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — create branch immediately
- **US1 (Phase 2)**: Depends on branch creation — T002 (test) first, then T003–T006
- **US2 (Phase 3)**: Depends on US1's markup change (T003) for implementation verification; its tests (T007, T008) can be written in parallel with US1's tests
- **US3 (Phase 4)**: Independent — verification-only; tests can be written in parallel with US1/US2
- **Polish (Phase 5)**: Depends on US1 (markup + CSS in place); T017–T019 depend on all tests being written; T020 last

### User Story Dependencies

- **US1 (P1)**: The core change — everything else verifies or extends it
- **US2 (P1)**: No new code expected; proves US1's shared-navbar edit covers authenticated users
- **US3 (P2)**: No code expected; pins existing root behavior

### Parallel Opportunities

- T002, T007, T008, T010, T011: all [P] — separate test blocks in the same new file; if dispatched to parallel subagents, each agent appends distinct `test(...)` blocks (or the file is created once by T002 and others append — see note below)
- T004 and T005: [P] — independent CSS rules in `site.css` (adjacent lines, so a single agent can do both)
- T013, T014, T015, T016: all [P] — independent edge-case tests
- T017 and T018: [P] — independent verification gates (but T017 needs the app restarted; T018 is build-only)
- Principle XI: T002/T007/T008 (test-writing) can be dispatched as parallel subagent runs; T003/T004/T005 (implementation) as parallel runs on distinct files (`_Layout.cshtml` vs `site.css`); parent session integrates and runs T006

> **Single-file note**: T002, T007–T008, T010–T011, T013–T016 all touch
> `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts`. For true parallel
> subagent execution, split the spec file per story (e.g., T002 creates the
> file with US1 tests; T007/T008 append US2 tests; T010/T011 append US3 tests;
> T013–T016 append polish tests) or have the parent session apply them
> sequentially. One writer per file at a time (Principle XI).

---

## Parallel Example: User Story 1

```text
# Test first (fails before implementation):
Task: "Create 11-brand-home-link.spec.ts with the signed-out Login-page brand test (T002)"

# Then implementation in parallel (distinct files):
Task: "Replace span.brand with a.brand href='/' in _Layout.cshtml (T003)"
Task: "Add text-decoration:none + hover rule to .navbar .brand in site.css (T004+T005)"

# Then sequentially:
Task: "Rebuild + restart app, verify Login page brand is a link (T006)"
Task: "Run T002 — now passes"
```

---

## Implementation Strategy

### MVP First (Phase 2 Only)

1. Complete Phase 1: branch creation
2. Complete Phase 2: T002 (failing test) → T003–T005 (one-line markup + two CSS rules) → T006 (restart)
3. **STOP and VALIDATE**: signed-out user on Login reaches Browse Courses in one brand click
4. This delivers the reported pain-point fix completely

### Incremental Delivery

1. US1 → Login page dead end removed (the user's exact complaint)
2. US2 → signed-in users verified across learner + admin sections (no new code expected)
3. US3 → Home = Browse Courses pinned as a test (no code expected)
4. Polish → edge cases + Principle XIII gates + merge to master

---

## Notes

- All code changes touch only `src/Host/Pages/Shared/_Layout.cshtml` (one line), `src/Host/wwwroot/css/site.css` (two rules), plus the new `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts`
- No server-side code changes, no module changes, no database changes, no new JS
- The root redirect (`GET /` → `/Courses`) already exists and is verified, not modified
- Commit after each phase for clean incremental history
- Per user instruction: after all tasks complete, merge directly to `master` (no PR); the user initiates `/speckit.implement`
