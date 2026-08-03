---

description: "Task list for Organic Design System Redesign"
---

# Tasks: Organic Design System Redesign

**Input**: Design documents from `/specs/017-organic-ui-redesign/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/page-routes.md](contracts/page-routes.md), [quickstart.md](quickstart.md)

**Tests**: Unit tests are included for the three new `EnrollmentService` methods (committed to in plan.md's source-tree diff); no new UI test project is introduced — visual/behavioral verification uses the manual `quickstart.md` scenarios plus the existing `ArchitectureTests`/module test suites for regression coverage (SC-005).

**Organization**: Tasks are grouped by user story (US1–US4, matching spec.md's P1–P4) so each can be implemented and validated independently.

## Path Conventions

Single ASP.NET Core modular-monolith project — paths are relative to repo root (`src/Host`, `src/Modules/*`, `tests/*`), per plan.md's Project Structure.

---

## Phase 1: Setup (Shared Assets)

**Purpose**: Assets every later phase depends on

- [ ] T001 [P] Add self-hosted Caprasimo webfont file(s) under `src/Host/wwwroot/fonts/caprasimo/` (per research.md §2)
- [ ] T002 [P] Add self-hosted Figtree webfont file(s) under `src/Host/wwwroot/fonts/figtree/` (per research.md §2)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Design tokens and shared nav chrome that every user story's pages render through

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — every restyled page (US1–US4) reads these tokens/classes

- [ ] T003 Add Organic color/typography/radius/shadow tokens to the `:root` block in `src/Host/wwwroot/css/site.css` (`--color-accent`, `--color-accent-2`, `--font-heading`, `--font-body`, `--radius-lg`, pill radius), retargeting existing token names per research.md §1 (depends on T001, T002 for font references)
- [ ] T004 Add `@font-face` declarations for Caprasimo/Figtree in `src/Host/wwwroot/css/site.css` referencing the files from T001/T002, with the existing system-font stack as fallback (depends on T003)
- [ ] T005 Add shared Organic component classes to `src/Host/wwwroot/css/site.css` — `.card`, `.tag`/`.tag-neutral`/`.tag-accent-2`/`.tag-outline`, `.btn`/`.btn-primary`/`.btn-secondary`/`.btn-ghost`, `.progress-track`/`.progress-fill` — replacing the current ad-hoc inline-styled badges/metric-cards (depends on T003)
- [ ] T006 Restyle the nav bar chrome (brand wordmark, page links, hamburger toggle) in `src/Host/Pages/Shared/_Layout.cshtml` to the Organic token/class set from T003–T005 — page link set and role-gating unchanged per spec Assumptions; the avatar/profile control is added later in US3 (depends on T005)
- [ ] T007 Verify the existing ≤760px hamburger collapse behavior (spec 015/016) still functions after T006's markup/class changes (depends on T006)

**Checkpoint**: Design tokens and nav chrome are in place — user story implementation can now begin

---

## Phase 3: User Story 1 - Learner browses and manages courses in the new visual system (Priority: P1) 🎯 MVP

**Goal**: My Courses, Browse Courses, and Course Detail render in the Organic visual system with all existing behavior (enrolled cards/status/progress, search/filter, enroll) intact.

**Independent Test**: Log in as a learner; view My Courses (enrolled cards + empty state), Browse Courses (search/filter/clear/no-results/enroll), and Course Detail (hero + Enroll now / ✓ Enrolled) — per quickstart.md scenarios 1–2.

### Implementation for User Story 1

- [ ] T008 [P] [US1] Restyle My Courses cards and empty state in `src/Host/Pages/MyCourses/Index.cshtml` and `src/Host/Pages/Shared/_EnrollmentList.cshtml` (category kicker, title, description, status tag, hours tag, pill progress bar, "Enrolled {date} · {pct}% complete" line; centered empty-state card + primary button to Browse Courses) per data-model.md's course-card mapping
- [ ] T009 [US1] Add `StatusTagClass`/`ProgressPercent` view-model fields to `src/Host/Pages/MyCourses/Index.cshtml.cs`, reusing `ScormHelpers.GetDisplayLabel` and mapping `LatestScore`/status to a 0–100 percentage per research.md §3 (depends on T008 for the tag-class names it populates)
- [ ] T010 [P] [US1] Restyle Browse Courses toolbar, card grid, and no-results state in `src/Host/Pages/Courses/Index.cshtml` (search input, category select, Clear button; category/hours/"✓ Enrolled" tags + "View Details" button; "No courses match your search." message)
- [ ] T011 [P] [US1] Restyle Course Detail hero and enroll CTA in `src/Host/Pages/Courses/Detail.cshtml` ("Back to courses" control, tag row, title, description, primary "Enroll now" / disabled "✓ Enrolled" button)
- [ ] T012 [US1] Run quickstart.md scenarios 1–2 (My Courses, Browse Courses search/filter/enroll) and confirm no functional regressions

**Checkpoint**: User Story 1 is fully functional and independently testable/demoable — this is the MVP slice

---

## Phase 4: User Story 2 - Admin views platform activity in the new visual system (Priority: P2)

**Goal**: Admin Dashboard shows existing scoped metrics as Organic stat tiles plus a new all-courses table with real per-course enrollment counts.

**Independent Test**: Log in as SuperUser and OrgAdmin; confirm scoped stat tiles and an accurate courses table — per quickstart.md scenario 3.

### Implementation for User Story 2

- [ ] T013 [US2] Add `GetEnrollmentCountsByCourseAsync(IEnumerable<Guid> courseIds)` to `src/Modules/Enrollment/Application/EnrollmentService.cs` — single grouped `COUNT(*)` query, per data-model.md
- [ ] T014 [P] [US2] Unit tests for `GetEnrollmentCountsByCourseAsync` in `tests/Enrollment.Tests` (multiple courses, a zero-enrollment course, empty input) (depends on T013)
- [ ] T015 [US2] Extend `src/Host/Pages/Admin/Dashboard/Index.cshtml.cs` to call `CourseVisibilityService.GetAllCoursesAsync` (Management) and the new `GetEnrollmentCountsByCourseAsync` (Enrollment), zipping them into an `AllCourses` row list (title, category, hours, enrollment count) (depends on T013)
- [ ] T016 [US2] Restyle `src/Host/Pages/Admin/Dashboard/Index.cshtml` — stat tiles (kicker label + large accent-700 number) and the new "All Courses" table (depends on T015)
- [ ] T017 [US2] Run quickstart.md scenario 3 (Dashboard stat tiles + course table) and spot-check one course's enrollment count against `/Admin/Enrollments`

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Any authenticated user manages their profile and settings (Priority: P3)

**Goal**: A profile dropdown (View Profile / Settings) replaces the inline username+Logout; Settings persists two real preferences and hosts Logout.

**Independent Test**: Open the profile dropdown, view profile info, change and persist Settings preferences, log out from Settings — per quickstart.md scenario 4.

### Implementation for User Story 3

- [ ] T018 [US3] Add `EmailNotificationsEnabled` (`bool`, default `true`) and `ThemePreference` (`string`, default `"System"`) to `src/Modules/Enrollment/Domain/Student.cs`
- [ ] T019 [US3] Configure the two new columns (not-null + defaults) in `src/Modules/Enrollment/Infrastructure/EnrollmentDbContext.cs` (depends on T018)
- [ ] T020 [US3] Add an EF Core migration under `src/Host/Migrations/` for the two new `Students` columns (depends on T019)
- [ ] T021 [P] [US3] Add `GetPreferencesAsync`/`UpdatePreferencesAsync` to `src/Modules/Enrollment/Application/EnrollmentService.cs` (depends on T018)
- [ ] T022 [P] [US3] Unit tests for `GetPreferencesAsync`/`UpdatePreferencesAsync` in `tests/Enrollment.Tests` (depends on T021)
- [ ] T023 [P] [US3] Create `src/Host/Pages/Account/Profile.cshtml` + `Profile.cshtml.cs` — name/role/email read from the current `ClaimsPrincipal`, in a bordered-row card, per contracts/page-routes.md
- [ ] T024 [US3] Create `src/Host/Pages/Account/Settings.cshtml` + `Settings.cshtml.cs` — `OnGetAsync`/`OnPostAsync` using T021's methods; email-notifications toggle + theme row + "Logout" row posting to the existing `/Account/Logout` handler (depends on T021)
- [ ] T025 [US3] Add the avatar+name profile control and its View Profile/Settings dropdown to `src/Host/Pages/Shared/_Layout.cshtml`, replacing the current inline username-and-Logout text (depends on T006, T023, T024)
- [ ] T026 [US3] Run quickstart.md scenario 4 (profile menu, view profile, settings persistence across reload, logout)

**Checkpoint**: User Stories 1, 2, and 3 all work independently

---

## Phase 6: User Story 4 - Mobile users get the same experience in the new visual system (Priority: P4)

**Goal**: All redesigned screens carry the existing hamburger/responsive mechanics through the new visual system, with the avatar control staying visible outside the hamburger.

**Independent Test**: Load each redesigned page at 375px and confirm nav collapse, heading sizes, stacked layouts, and no horizontal scroll — per quickstart.md scenario 5.

### Implementation for User Story 4

- [ ] T027 [US4] Verify/adjust ≤760px responsive rules in `src/Host/wwwroot/css/site.css` for the six redesigned screens (heading size, toolbar/hero stacking) now that T008–T024's markup is in place (depends on T011, T016, T024)
- [ ] T028 [US4] Confirm the avatar/profile control (T025) remains visible outside the hamburger at ≤760px per research.md §6, adjusting `_Layout.cshtml` mobile CSS if needed (depends on T025, T027)
- [ ] T029 [US4] Run quickstart.md scenario 5 at 375px across all six redesigned screens

**Checkpoint**: All four user stories are independently functional, including on mobile

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Regression safety and cleanup across all stories

- [ ] T030 [P] Run `dotnet test tests/ArchitectureTests` to confirm no module-boundary violations were introduced (Constitution Principle III)
- [ ] T031 [P] Run `dotnet test tests/Catalog.Tests tests/Enrollment.Tests tests/Scorm.Tests` to confirm no functional regressions (SC-005)
- [ ] T032 Remove now-superseded inline hex-color styles (e.g., the old Dashboard `metric-card`/badge inline colors, `ScormHelpers.GetStatusBadgeColors` call sites in views replaced by Organic tag classes) across touched files
- [ ] T033 Run the full quickstart.md validation end-to-end and record results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup (T001–T002) — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational only
- **User Story 2 (Phase 4)**: Depends on Foundational only (independent of US1)
- **User Story 3 (Phase 5)**: Depends on Foundational only (independent of US1/US2, but T025 also touches `_Layout.cshtml` from T006 — sequenced after T006, not after US1/US2)
- **User Story 4 (Phase 6)**: Depends on the page markup produced by US1/US2/US3 (T011, T016, T024, T025) since it verifies/adjusts their responsive behavior
- **Polish (Phase 7)**: Depends on all four user stories being complete

### User Story Dependencies

- **US1 (P1)**: No dependency on US2/US3/US4 — can be built, demoed, and shipped alone as the MVP
- **US2 (P2)**: No dependency on US1 — independently testable once Foundational is done
- **US3 (P3)**: No dependency on US1/US2 — independently testable once Foundational is done
- **US4 (P4)**: Depends on US1+US2+US3's markup existing (it verifies their responsive behavior) — must run last

### Parallel Opportunities

- T001, T002 (Setup) — different font families, fully parallel
- T014, T021, T022, T023 — different files from each other and from concurrently-running story work
- Once Foundational (Phase 2) completes, **US1 and US2 can be staffed in parallel** (no shared files); US3 can also start in parallel but its `_Layout.cshtml` task (T025) should land after US1/US2 are stable to avoid nav-file churn colliding with T006
- T030, T031 (Polish) — independent test suites, fully parallel

---

## Parallel Example: User Story 1

```bash
# After Foundational (Phase 2) completes, launch independent US1 view work together:
Task: "Restyle My Courses cards and empty state in src/Host/Pages/MyCourses/Index.cshtml and _EnrollmentList.cshtml"
Task: "Restyle Browse Courses toolbar/grid/no-results in src/Host/Pages/Courses/Index.cshtml"
Task: "Restyle Course Detail hero and enroll CTA in src/Host/Pages/Courses/Detail.cshtml"
```

## Parallel Example: Cross-Story (post-Foundational)

```bash
# US1, US2, and US3's backend/domain work can proceed in parallel — no shared files:
Task: "[US1] Restyle Browse Courses in src/Host/Pages/Courses/Index.cshtml"
Task: "[US2] Add GetEnrollmentCountsByCourseAsync to src/Modules/Enrollment/Application/EnrollmentService.cs"
Task: "[US3] Add EmailNotificationsEnabled/ThemePreference to src/Modules/Enrollment/Domain/Student.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run quickstart.md scenarios 1–2 independently
5. Demo: learner-facing redesign is live even before Dashboard/Profile/Settings exist

### Incremental Delivery

1. Setup + Foundational → tokens/nav chrome ready
2. US1 (P1) → validate → demo (MVP)
3. US2 (P2) → validate → demo
4. US3 (P3) → validate → demo
5. US4 (P4) → validate all six screens on mobile → demo
6. Polish → regression suite + cleanup

### Parallel Team Strategy

With multiple subagents (Constitution Principle XI — parallel implementation is mandatory where tasks are marked `[P]`):

1. One agent completes Setup + Foundational (sequential — mostly single-file `site.css`/`_Layout.cshtml` edits)
2. Once Foundational is done, dispatch in parallel: Agent A → US1 (T008–T012), Agent B → US2 (T013–T017), Agent C → US3's domain/service work (T018–T024)
3. A single agent applies T025 (`_Layout.cshtml` profile control) after US1/US2 have landed to avoid nav-file conflicts
4. US4 and Polish run last, after all three stories' markup exists

---

## Notes

- `[P]` tasks touch different files with no unmet dependency — same-file edits (e.g., the three `site.css` foundational tasks, or `_Layout.cshtml`'s T006/T025) are intentionally sequential to avoid merge conflicts between parallel subagents.
- `[Story]` labels map every Phase 3+ task to its spec.md user story for traceability.
- No contract/integration test tasks were generated for the Razor Page routes themselves (no test framework for Razor Page rendering exists in this repo yet); regression safety instead comes from the existing `ArchitectureTests`/module suites (T030–T031) plus the manual `quickstart.md` scenarios per story.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently before continuing.
