# Tasks: Admin Menu Structure

**Input**: Design documents from `/specs/021-admin-menu-structure/`

**Prerequisites**: plan.md, spec.md, research.md

## Phase 1: Setup

- [X] T001 Create branch `story/021-admin-menu-structure` from `main`

---

## Phase 2: User Story 1 — Admin discovers all management sections (P1) 🎯 MVP

**Goal**: Add nav links for Courses, Enrollments, Learners, Organizations, and Upload to the main navbar (Dashboard already exists).

**Independent Test**: Log in as admin, switch to Admin mode, verify all 6 admin links are visible and navigate correctly.

### Implementation

- [X] T002 [P] Add 5 admin nav link `<a>` tags to `src/Host/Pages/Shared/_Layout.cshtml` (after the existing Dashboard link, each with `data-role-link="admin"`, `data-page`, Lucide icon, and `asp-page` pointing to the correct admin page)
- [X] T003 [P] Update the JS `linkMap` object in `src/Host/Pages/Shared/_Layout.cshtml` with paths for `/Admin/Courses/Index`, `/Admin/Enrollments/Index`, `/Admin/Learners/Index`, `/Admin/Organizations/Index`, `/Admin/Upload`
- [X] T004 Verify that existing CSS (`[data-role-link="admin"]` selectors) already handles visibility toggle for the new links without changes to `src/Host/wwwroot/css/site.css`

---

## Phase 3: User Story 2 — Learner view hides admin sections (P2)

**Goal**: Confirm the existing Learner/Admin toggle already hides all new admin links.

**Independent Test**: Toggle role pill between Learner/Admin and verify all 6 admin links show/hide together.

### Implementation

- [X] T005 Manual verification — no code change expected. The `[data-role-link="admin"]` CSS selector on all new links (added in T002) is sufficient. Confirm by browser test.

---

## Phase 4: User Story 3 — Menu persists across UI changes (P3)

**Goal**: The spec itself is the durable reference. No code change needed.

**Independent Test**: Confirm `specs/021-admin-menu-structure/spec.md` lists all 7 functional requirements (FR-001 through FR-007).

### Implementation

- [X] T006 Verify spec.md is complete (already done in `/speckit.specify` step)

---

## Phase 5: Validation

- [X] T007 Run `dotnet build` and confirm no compilation errors
- [X] T008 Run `dotnet test tests/ArchitectureTests` and confirm architecture tests pass (2 pre-existing failures unrelated to this spec)
- [X] T009 Run the app (`dotnet run`) and validate all 5 scenarios from `quickstart.md`
- [X] T010 Commit changes, push branch, merge to `main`
- [X] T011 Switch back to `main` (Constitution Principle XII)

---

## Dependencies & Execution Order

- **T001**: No dependencies — start immediately
- **T002, T003**: Both edit `_Layout.cshtml` but touch different regions (HTML vs JS). Can be done sequentially in one edit pass.
- **T004**: Depends on T002 (links must exist to verify)
- **T005**: Depends on T002 (manual browser test)
- **T006**: No dependencies (spec already written)
- **T007–T011**: Depend on all implementation tasks

### Parallel Opportunities

T002 and T003 can be combined into a single file edit (they're in the same file, different regions).
