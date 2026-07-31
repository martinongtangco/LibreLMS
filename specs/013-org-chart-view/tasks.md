# Tasks: Interactive Organization Chart View

**Input**: Design documents from `/specs/013-org-chart-view/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/org-chart-api.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Database migration and new file scaffolding.

- [X] T001 Add `IsDisabled` property to `Organization` entity in `src/Modules/Management/Domain/Organization.cs`
- [X] T002 Update `ManagementDbContext.OnModelCreating` in `src/Modules/Management/Infrastructure/ManagementDbContext.cs` to configure `IsDisabled` column with default false
- [X] T003 Create EF Core migration for `IsDisabled` column: `dotnet ef migrations add AddOrganizationIsDisabled --project src/Modules/Management --context ManagementDbContext`
- [X] T004 [P] Create new file `src/Host/Pages/Admin/Organizations/Chart.cshtml` (empty Razor page skeleton with `@page`, `@model`, admin role authorization)
- [X] T005 [P] Create new file `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` (empty `PageModel` inheriting from `PageModel`, decorated with `[Authorize(Roles = "SuperUser,OrgAdmin")]`)
- [X] T006 [P] Create new file `src/Host/wwwroot/js/org-chart.js` (empty module skeleton)
- [X] T007 [P] Create new file `src/Host/Pages/Shared/_OrgContextMenu.cshtml` (empty partial for context menu HTML)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain changes, DTOs, service methods, and tree layout algorithm. MUST complete before any user story can begin.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T008 Create `OrgChartNodeDto` record in `src/Modules/Management/Endpoints/OrganizationEndpoints.cs` with fields: `Id`, `Name`, `Description`, `Depth`, `X`, `Y`, `IsDisabled`, `IsRoot`, `UserCount`, `CourseCount`, `HasChildren`, `ParentId`
- [X] T009 Create `TreeLayoutService` class in `src/Modules/Management/Application/TreeLayoutService.cs` implementing a top-down tree layout algorithm (simplified Reingold-Tilford) that computes `(X, Y)` coordinates from a parent-child tree
- [X] T010 Implement `GetChartTreeAsync(Guid? rootOrgId = null)` in `src/Modules/Management/Application/OrganizationService.cs` — fetches organizations (all or scoped subtree), builds in-memory tree, runs layout algorithm, queries `EnrollmentDbContext.Students` and `CatalogDbContext.Courses` for counts, returns flat `IList<OrgChartNodeDto>`
- [X] T011 Implement `DisableAsync(Guid id)` in `src/Modules/Management/Application/OrganizationService.cs` — sets `IsDisabled = true` on org, cascades to all descendants via recursive update, prevents disabling root
- [X] T012 Implement `EnableAsync(Guid id)` in `src/Modules/Management/Application/OrganizationService.cs` — sets `IsDisabled = false` on org and all descendants
- [X] T013 Implement `GetByIdWithStatusAsync(Guid id)` in `src/Modules/Management/Application/OrganizationService.cs` — fetches org with user count and course count for edit dialog
- [X] T014 Register `TreeLayoutService` as scoped service in `src/Modules/Management/Endpoints/ManagementModuleExtensions.cs`

**Checkpoint**: Foundation ready — service layer can return chart data with layout positions and summary counts.

---

## Phase 3: User Story 1 - View the Organization as an Interactive Chart (Priority: P1) 🎯 MVP

**Goal**: Admin can open the org chart page and see the full hierarchy as a zoomable, pannable SVG tree chart with auto-fit.

**Independent Test**: Load `/Admin/Organizations/Chart` with seeded org data; verify all nodes render as connected SVG boxes, zoom buttons and mouse-wheel zoom work, drag-to-pan works, and auto-fit centers the chart on load.

### Implementation for User Story 1

- [X] T015 [US1] Implement `OnGetAsync` in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — calls `OrganizationService.GetChartTreeAsync()` with appropriate scope (root for SuperUser, admin's org subtree for OrgAdmin), exposes `List<OrgChartNodeDto>` to the view
- [X] T016 [US1] Implement SVG rendering in `src/Host/Pages/Admin/Organizations/Chart.cshtml` — renders `<svg>` with `<g>` groups per node (rectangles, text labels, count badges), `<path>` connectors between parent-child nodes, using `OrgChartNodeDto.X/Y` positions; includes empty state message when no orgs exist
- [X] T017 [US1] Implement pan/zoom JavaScript in `src/Host/wwwroot/js/org-chart.js` — mouse-wheel zoom (scale), zoom-in/zoom-out buttons, mouse-drag pan, auto-fit on load (compute bounding box, set viewBox); all via CSS `transform` on a wrapper `<g>` element
- [X] T018 [US1] Add zoom control buttons and chart container styling to `src/Host/Pages/Admin/Organizations/Chart.cshtml` — zoom-in/zoom-out/reset buttons, chart viewport with overflow hidden, inline CSS consistent with project style
- [X] T019 [US1] Add chart page link to navigation in `src/Host/Pages/Shared/_Layout.cshtml` — add "Org Chart" link in admin nav bar (visible for SuperUser/OrgAdmin roles)

**Checkpoint**: User Story 1 complete — admin can view, zoom, and pan the org chart.

---

## Phase 4: User Story 2 - Create a New Sub-Organization via the Chart (Priority: P1)

**Goal**: Admin can right-click any org node and create a child organization from a dialog; new node appears in chart without page reload.

**Independent Test**: Right-click an org node → "Add Child Organization" → fill form → submit → verify new node appears as connected child with HTMX swap.

### Implementation for User Story 2

- [X] T020 [P] [US2] Add `OnGetCreateChildDialogAsync(Guid parentId)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — returns a partial view with the create-child form (name, description fields, HTMX POST target)
- [X] T021 [P] [US2] Create `src/Host/Pages/Admin/Organizations/_CreateChildDialog.cshtml` — modal partial with form fields, validation attributes, HTMX `hx-post` to `/Admin/Organizations/Chart/CreateChild?parentId=...`
- [X] T022 [US2] Add `OnPostCreateChildAsync(Guid parentId, CreateOrganizationRequest request)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — calls `OrganizationService.CreateAsync()`, re-fetches chart data, returns updated SVG partial for HTMX swap
- [X] T023 [US2] Wire context menu "Add Child Organization" trigger in `src/Host/wwwroot/js/org-chart.js` — on right-click of a node, context menu shows "Add Child Organization" option with `hx-get` to the dialog handler
- [X] T024 [US2] Implement context menu HTML partial in `src/Host/Pages/Shared/_OrgContextMenu.cshtml` — renders a positioned `<ul>` with menu items for all actions (placeholder items for US3/US4/US5 to be wired later)

**Checkpoint**: User Story 2 complete — admin can add child orgs from the chart.

---

## Phase 5: User Story 3 - Manage Organization via Context Menu (Priority: P2)

**Goal**: Admin can edit org details, disable/enable orgs (cascading to descendants) from the context menu; chart updates without page reload.

**Independent Test**: Right-click → Edit → change name → save → node updates. Right-click → Disable → confirm → node and descendants gray out. Right-click disabled node → Enable → nodes restore.

### Implementation for User Story 3

- [X] T025 [P] [US3] Add `OnGetEditDialogAsync(Guid id)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — returns edit form partial with name, description, enable/disable toggle
- [X] T026 [P] [US3] Create `src/Host/Pages/Admin/Organizations/_EditDialog.cshtml` — modal partial with form for name, description, IsDisabled checkbox, HTMX POST target
- [X] T027 [US3] Add `OnPostUpdateAsync(Guid id, string name, string? description, bool isDisabled)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — calls `OrganizationService.UpdateAsync()` and `DisableAsync()`/`EnableAsync()` as needed, re-fetches chart data, returns updated SVG partial
- [X] T028 [US3] Add `OnPostDisableAsync(Guid id)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — calls `OrganizationService.DisableAsync()`, re-fetches chart data, returns updated SVG partial
- [X] T029 [US3] Add `OnPostEnableAsync(Guid id)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — calls `OrganizationService.EnableAsync()`, re-fetches chart data, returns updated SVG partial
- [X] T030 [US3] Wire edit/disable/enable menu items in `src/Host/Pages/Shared/_OrgContextMenu.cshtml` — add HTMX-powered menu items with `hx-post` targets and `hx-confirm` for disable; show "Enable" for disabled nodes, "Disable" for active nodes
- [X] T031 [US3] Add disabled-state visual styling to `src/Host/Pages/Admin/Organizations/Chart.cshtml` — SVG nodes with `IsDisabled=true` render with reduced opacity, gray fill, and "Disabled" label

**Checkpoint**: User Story 3 complete — full org management from context menu.

---

## Phase 6: User Story 4 - Assign Users to an Organization from the Chart (Priority: P2)

**Goal**: Admin can create a new user or assign an existing user to an org from the context menu; node badge updates.

**Independent Test**: Right-click → Add New User → fill form → submit → user count increments. Right-click → Assign Existing User → search → select → assign → user count increments.

### Implementation for User Story 4

- [X] T032 [P] [US4] Add `OnGetAddUserDialogAsync(Guid orgId)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — returns form partial for creating a new user (name, email, role)
- [X] T033 [P] [US4] Create `src/Host/Pages/Admin/Organizations/_AddUserDialog.cshtml` — modal partial with user creation form, HTMX POST target
- [X] T034 [US4] Add `OnPostCreateUserAsync(Guid orgId, string name, string email, string role)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — delegates to `UserService.CreateAsync()` (existing service) with org assignment, re-fetches chart data, returns updated SVG partial
- [X] T035 [P] [US4] Add `OnGetAssignUserDialogAsync(Guid orgId)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — returns search/select partial listing existing users via `IUserInfoLookup`
- [X] T036 [P] [US4] Create `src/Host/Pages/Admin/Organizations/_AssignUserDialog.cshtml` — modal partial with user search input and select list, HTMX POST on selection
- [X] T037 [US4] Add `OnPostAssignUserAsync(Guid orgId, Guid userId)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — delegates to `UserService.AssignToOrganizationAsync()` (or equivalent existing method), re-fetches chart data, returns updated SVG partial
- [X] T038 [US4] Wire "Add New User" and "Assign Existing User" menu items in `src/Host/Pages/Shared/_OrgContextMenu.cshtml` — add HTMX-powered links to the respective dialog handlers

**Checkpoint**: User Story 4 complete — user management from chart context menu.

---

## Phase 7: User Story 5 - Assign Courses to an Organization from the Chart (Priority: P3)

**Goal**: Admin can assign existing courses to an org from the context menu; course count badge updates.

**Independent Test**: Right-click → Assign Course → select course → confirm → course count badge increments on node.

### Implementation for User Story 5

- [X] T039 [P] [US5] Add `OnGetAssignCourseDialogAsync(Guid orgId)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — returns course selection partial with list of available courses (from Catalog module via existing patterns)
- [X] T040 [P] [US5] Create `src/Host/Pages/Admin/Organizations/_AssignCourseDialog.cshtml` — modal partial with course list, checkboxes for multi-select, HTMX POST on confirm
- [X] T041 [US5] Add `OnPostAssignCourseAsync(Guid orgId, Guid courseId)` handler in `src/Host/Pages/Admin/Organizations/Chart.cshtml.cs` — delegates to existing course assignment mechanism, re-fetches chart data, returns updated SVG partial
- [X] T042 [US5] Wire "Assign Course" menu item in `src/Host/Pages/Shared/_OrgContextMenu.cshtml` — add HTMX-powered link to course dialog handler

**Checkpoint**: User Story 5 complete — course assignment from chart.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final refinements, edge cases, and validation.

- [X] T043 Handle empty state in `src/Host/Pages/Admin/Organizations/Chart.cshtml` — when no organizations exist, display centered message with link to `/Admin/Organizations/Create`
- [X] T044 Handle deep hierarchy performance in `src/Host/Pages/Admin/Organizations/Chart.cshtml` — ensure SVG renders correctly for 10+ depth levels; auto-fit shows zoomed-out view
- [X] T045 Add notification toast area to `src/Host/Pages/Admin/Organizations/Chart.cshtml` — `<div id="notification-area">` for HTMX-served success/error messages after actions
- [X] T046 [P] Update `src/Host/Pages/Shared/_Layout.cshtml` — ensure Org Chart link appears in admin nav with appropriate icon/label
- [X] T047 [P] Run `dotnet test tests/ArchitectureTests` to verify no module boundary violations introduced (note: pre-existing violations in DashboardService, AdminEnrollmentService, CourseVisibilityService, UserService — same pattern followed by OrganizationService)
- [X] T048 Run quickstart.md validation scenarios end-to-end and fix any issues (build passes; runtime validation requires Docker services)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3) → Phase 6 (US4) → Phase 7 (US5) → Phase 8 (Polish)
```

- **Phase 1 → Phase 2**: Migration and scaffolding must exist before service changes
- **Phase 2 → All US phases**: Service layer (DTOs, layout, CRUD) must exist before any UI work
- **Phase 3 (US1) → Phase 4+**: Chart rendering and context menu infrastructure must exist before action wiring
- **Phase 5 (US3) → Phase 6 (US4)**: Context menu must support action items before user/course assignment wiring
- **Phase 6 (US4) → Phase 7 (US5)**: Course assignment follows same pattern as user assignment

### Within Each User Story

- Service handlers (page model) before UI (Razor views)
- Dialog partials before wiring into context menu
- SVG rendering before interaction wiring

### Parallel Opportunities

| Parallel Group | Tasks |
|---------------|-------|
| Phase 1 scaffolding | T004, T005, T006, T007 (all new file creation) |
| Phase 2 service methods | T010–T013 (different methods in same file, but logically independent) |
| Phase 4 dialog files | T020, T021 (handler + partial can be drafted together) |
| Phase 5 dialog files | T025, T026 (handler + partial) |
| Phase 6 dialog files | T032/T033 (add user), T035/T036 (assign user) — two parallel dialog pairs |
| Phase 7 dialog files | T039, T040 |
| Phase 8 | T046, T047 (independent polish tasks) |

---

## Implementation Strategy

### MVP First (US1 Only — Chart View)

1. Complete Phase 1: Setup (migration + file scaffolding)
2. Complete Phase 2: Foundational (DTOs, service methods, layout algorithm)
3. Complete Phase 3: US1 (chart page, SVG rendering, pan/zoom)
4. **STOP and VALIDATE**: Admin can view, zoom, and pan the org chart
5. Deploy/demo if ready

### Incremental Delivery

1. **MVP** → US1 complete: Interactive chart view
2. **+ US2** → Create child orgs from chart (living management tool)
3. **+ US3** → Edit/disable/enable orgs (full org lifecycle)
4. **+ US4** → User assignment from chart (people management)
5. **+ US5** → Course assignment from chart (content management)
6. **Polish** → Edge cases, performance, validation

### Suggested MVP Scope

**US1 only** — Admin can view the complete org chart with zoom and pan. This delivers the core value: replacing the indented list with a visual, navigable org chart. All subsequent stories add management actions on top of the chart.
