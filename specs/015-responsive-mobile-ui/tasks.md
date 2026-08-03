# Tasks: Responsive Mobile UI

**Input**: Design documents from `/specs/015-responsive-mobile-ui/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Tests**: No unit tests required — validation is visual/manual per quickstart.md.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup — CSS Foundation

**Purpose**: Create the CSS foundation with design tokens and responsive breakpoints.

 - [X] T001 Create `src/Host/wwwroot/css/site.css` with CSS custom properties (design tokens) extracted from `_Layout.cshtml` inline styles
 - [X] T002 Add responsive media queries to `site.css` for 480px (mobile), 768px (tablet), 1024px (desktop) breakpoints
 - [X] T003 Add hamburger menu CSS (toggle button, mobile nav panel, transitions) to `site.css`
 - [X] T004 Add data table responsive wrapper styles to `site.css`
 - [X] T005 Add form element responsive styles (full-width inputs, touch targets) to `site.css`
 - [X] T006 Add dashboard metric card grid responsive styles to `site.css`
 - [X] T007 Add modal/dialog responsive styles to `site.css`
 - [X] T008 Add utility classes (flex, spacing, text) to `site.css`

---

## Phase 2: Layout — Global Changes

**Purpose**: Convert the layout to use the new stylesheet and add hamburger navigation.

 - [X] T009 [US1] Modify `src/Host/Pages/Shared/_Layout.cshtml`: replace inline `<style>` with `<link href="/css/site.css" rel="stylesheet" />`, add hamburger toggle button markup, add vanilla JS toggle script
 - [X] T010 [US1] Modify `src/Host/Pages/Shared/_Layout.cshtml`: convert navbar to responsive layout — full nav on desktop, collapsible on mobile ≤ 480px

---

## Phase 3: Student-Facing Pages (US1 - P1)

**Purpose**: Make browse, detail, and enrollment pages mobile-friendly.

### Browse & Course Catalog

 - [X] T011 [P] [US1] Modify `src/Host/Pages/Courses/Index.cshtml`: replace inline styles with class names
 - [X] T012 [P] [US1] Modify `src/Host/Pages/Shared/_CourseCard.cshtml`: replace inline styles with class names, ensure single-column on mobile
 - [X] T013 [P] [US1] Modify `src/Host/Pages/Shared/_CourseList.cshtml`: replace inline styles with class names

### Course Detail & Enrollment

 - [X] T014 [P] [US1] Modify `src/Host/Pages/Courses/Detail.cshtml`: replace inline styles with class names, ensure stacked layout on mobile, touch-friendly enroll button
 - [X] T015 [P] [US1] Modify `src/Host/Pages/Shared/_CourseDetail.cshtml`: replace inline styles with class names
 - [X] T016 [P] [US1] Modify `src/Host/Pages/Shared/_EnrollmentResult.cshtml`: replace inline styles with class names

### My Courses

 - [X] T017 [P] [US4] Modify `src/Host/Pages/MyCourses/Index.cshtml`: replace inline styles with class names
 - [X] T018 [P] [US4] Modify `src/Host/Pages/Shared/_EnrollmentList.cshtml`: replace inline styles with class names
 - [X] T019 [P] [US4] Modify `src/Host/Pages/Shared/_MyCourseRow.cshtml`: replace inline styles with class names, ensure stacked layout on mobile

---

## Phase 4: Authentication Pages (US3 - P3)

**Purpose**: Make login page mobile-friendly.

 - [X] T020 [P] [US3] Modify `src/Host/Pages/Account/Login.cshtml`: replace inline styles with class names, ensure full-width inputs and button
 - [X] T021 [P] [US3] Modify `src/Host/Pages/Account/Logout.cshtml`: no changes needed (auto-redirect page, no layout)

---

## Phase 5: Admin Dashboard (US2 - P2)

**Purpose**: Make admin dashboard tablet/mobile-friendly.

 - [X] T022 [US2] Modify `src/Host/Pages/Admin/Dashboard/Index.cshtml`: replace inline styles with class names, metric cards reflow to 2-column grid at 768px

---

## Phase 6: Admin Learner Management (US2 - P2)

**Purpose**: Make learner management pages mobile-friendly.

 - [X] T023 [P] [US2] Modify `src/Host/Pages/Admin/Learners/Index.cshtml`: replace inline styles with class names, table wrapper with horizontal scroll
 - [X] T024 [P] [US2] Modify `src/Host/Pages/Admin/Learners/Create.cshtml`: replace inline styles with class names, form responsive layout
 - [X] T025 [P] [US2] Modify `src/Host/Pages/Admin/Learners/Edit.cshtml`: replace inline styles with class names, form responsive layout

---

## Phase 7: Admin Organization Management (US2 - P2)

**Purpose**: Make organization pages mobile-friendly.

 - [X] T026 [US2] Modify `src/Host/Pages/Admin/Organizations/Index.cshtml`: replace inline styles with class names
 - [X] T027 [US2] Modify `src/Host/Pages/Shared/_OrgNode.cshtml`: replace inline styles with class names
 - [X] T028 [P] [US2] Modify `src/Host/Pages/Shared/_OrgBreadcrumb.cshtml`: replace inline styles with class names, wrap on mobile
 - [X] T029 [P] [US2] Modify `src/Host/Pages/Shared/_OrgContextMenu.cshtml`: replace inline styles with class names
 - [X] T030 [US2] Modify `src/Host/Pages/Admin/Organizations/Chart.cshtml`: replace inline styles with class names, responsive chart container
 - [X] T031 [US2] Modify `src/Host/Pages/Shared/_OrgChartSvg.cshtml`: ensure SVG chart scrolls on mobile
 - [X] T032 [P] [US2] Modify `src/Host/Pages/Admin/Organizations/Create.cshtml`: replace inline styles with class names
 - [X] T033 [P] [US2] Modify `src/Host/Pages/Admin/Organizations/Edit.cshtml`: replace inline styles with class names
 - [X] T034 [P] [US2] Modify `src/Host/Pages/Admin/Organizations/_EditDialog.cshtml`: replace inline styles with class names, modal responsive
 - [X] T035 [P] [US2] Modify `src/Host/Pages/Admin/Organizations/_AddUserDialog.cshtml`: replace inline styles with class names, modal responsive
 - [X] T036 [P] [US2] Modify `src/Host/Pages/Admin/Organizations/_AssignCourseDialog.cshtml`: replace inline styles with class names, modal responsive
 - [X] T037 [P] [US2] Modify `src/Host/Pages/Admin/Organizations/_AssignUserDialog.cshtml`: replace inline styles with class names, modal responsive
 - [X] T038 [P] [US2] Modify `src/Host/Pages/Admin/Organizations/_CreateChildDialog.cshtml`: replace inline styles with class names, modal responsive

---

## Phase 8: Admin Course & Enrollment Management (US2 - P2)

**Purpose**: Make remaining admin pages mobile-friendly.

 - [X] T039 [P] [US2] Modify `src/Host/Pages/Admin/Courses/Index.cshtml`: replace inline styles with class names, table wrapper with horizontal scroll
 - [X] T040 [P] [US2] Modify `src/Host/Pages/Admin/Courses/Create.cshtml`: replace inline styles with class names, form responsive
 - [X] T041 [P] [US2] Modify `src/Host/Pages/Admin/Enrollments/Index.cshtml`: replace inline styles with class names, table wrapper with horizontal scroll
 - [X] T042 [P] [US2] Modify `src/Host/Pages/Admin/Enrollments/BulkEnroll.cshtml`: replace inline styles with class names, form responsive
 - [X] T043 [P] [US2] Modify `src/Host/Pages/Admin/Upload.cshtml`: replace inline styles with class names, form responsive

---

## Phase 9: SCORM & Error Pages

**Purpose**: Make SCORM launch and error pages mobile-friendly.

 - [X] T044 [P] [US4] Modify `src/Host/Pages/Scorm/Launch.cshtml`: ensure iframe container is responsive, status bar adapts to mobile
 - [X] T045 [P] Modify `src/Host/Pages/Error.cshtml`: replace inline styles with class names
 - [X] T046 [P] Modify `src/Host/Pages/Shared/_ErrorPartial.cshtml`: replace inline styles with class names

---

## Phase 10: Validation & Polish

**Purpose**: Final validation and cleanup.

 - [X] T047 Verify build succeeds: `cd src/Host && dotnet build`
 - [X] T048 Verify no inline `style=""` attributes remain in student-facing pages (Courses, MyCourses, Account/Login)
 - [X] T049 Verify no inline `style=""` attributes remain in admin pages
 - [X] T050 Cross-check desktop parity at 1280px — ensure layout matches original design
 - [X] T051 Create ADR `docs/adr/001-responsive-css-organization.md` documenting breakpoint strategy and CSS organization decision

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Layout (Phase 2)**: Depends on Phase 1 (needs site.css) — BLOCKS all page changes
- **Student Pages (Phase 3)**: Depends on Phase 2 — can begin in parallel after layout
- **Auth Pages (Phase 4)**: Depends on Phase 2 — can run in parallel with Phase 3
- **Admin Dashboard (Phase 5)**: Depends on Phase 2 — can run in parallel with Phase 3/4
- **Admin Learner (Phase 6)**: Depends on Phase 2 — can run in parallel
- **Admin Org (Phase 7)**: Depends on Phase 2 — can run in parallel
- **Admin Course/Enrollment (Phase 8)**: Depends on Phase 2 — can run in parallel
- **SCORM & Error (Phase 9)**: Depends on Phase 2 — can run in parallel
- **Validation (Phase 10)**: Depends on all previous phases

### Parallel Opportunities

- All Phase 1 tasks T001-T008 are writing to the same file (`site.css`) — must run sequentially
- Phase 2 tasks T009-T010 both modify `_Layout.cshtml` — must run sequentially
- **After Phase 2**, all remaining phases (3-9) can run in parallel since they touch different files
- Within each phase, tasks marked [P] can run in parallel

---

## Implementation Strategy

1. Complete Phase 1: CSS foundation (site.css with tokens, breakpoints, hamburger nav)
2. Complete Phase 2: Layout changes (remove inline style, add link, hamburger nav)
3. Run phases 3-9 in parallel (student pages, auth, admin dashboard, admin learners, admin org, admin courses, SCORM/errors)
4. Complete Phase 10: Build verification and polish
