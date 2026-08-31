# Bug Fix Specification: Learner Catalog Shows Courses Hidden by an Org Admin

**Feature Branch**: `bug/047-hide-course-visibility-not-enforced`

**Created**: 2026-07-30

**Status**: Complete (merged 2026-07-30, post-merge full suite 170/170, 1 documented verify-email skip)

**Input**: Workspace code review (2026-07-30). Spec 009 (RBAC management portal)
scenario 5: *"Given a learner in Organization B views their available courses,
When they browse the catalog, Then they see courses from their organization and
all ancestor organizations (unless hidden by an admin)."* The "unless hidden"
half is not enforced anywhere in the learner-facing catalog.

## Root Cause

`CourseVisibilityService.GetVisibleCoursesAsync(orgId)` returns every
org+ancestor course with an `IsHidden` flag (flag set from that org's
`CourseVisibilityOverrides`). The write path works
(`PUT /api/admin/courses/{id}/visibility` → `SetVisibilityOverrideAsync`),
but **no reader consumes the flag**:

- `src/Host/Pages/Courses/Index.cshtml.cs` `GetPagedCourses` (line ~108):
  `visibleCourseIds = visible.Select(v => v.CourseId).ToHashSet()` — hidden
  courses are passed straight into the browse filter, so they render.
- same file `GetCategoriesAsync` (line ~153): builds the category dropdown
  from the same unfiltered set.

Grep evidence: `IsHidden` appears nowhere under `src/Host/Pages` — the flag
is computed and dropped. (The SuperUser admin course list hardcodes
`"Visible"`/`"Local"` per row; that global list is a different surface —
see Scope.)

## Fix

**Consume the flag at the learner-facing read path**
(`src/Host/Pages/Courses/Index.cshtml.cs`, both spots):

- `GetPagedCourses`:
  `visible.Where(v => !v.IsHidden).Select(v => v.CourseId).ToHashSet()`.
- `GetCategoriesAsync`: build the id dictionary from
  `visible.Where(v => !v.IsHidden)` only.

This fixes both the full-page load and the HTMX `OnGetCourseListAsync`
partial (same code path). One sentence: "the browse page must exclude
courses the org admin marked hidden, using the flag the visibility service
already computes."

**E2E regression test** (new `tests/Playwright.Tests/tests/19-course-visibility.spec.ts`):
the seed data has a single root org (all users/courses root-owned), and a
course can only be hidden from a *child* org that inherits it
(`SetVisibilityOverrideAsync` refuses locally-owned courses) — so the test
builds the inheritance path per run:

1. SuperUser creates a unique child org under root (`POST /api/organizations`)
   and a unique verified learner in it (`POST /api/users`, admin-created ⇒
   `isVerified: true`).
2. Learner browses `/Courses/Index` → a chosen seeded root course (inherited)
   is visible.
3. SuperUser `PUT /api/admin/courses/{courseId}/visibility?organizationId={child}&isHidden=true`.
4. Learner browses → the course is gone; other root courses remain.
5. Unhide (`isHidden=false`) → the course is back.
6. `finally`: delete the learner, then the child org (no FK on the overrides
   table; the leftover `IsHidden=false` override row is inert).

## Scope

- **In**: learner browse list + category dropdown (spec 009 scenario 5).
- **Out (documented, not a defect for this slice)**:
  - OrgAdmin **dashboard** keeps listing all org+ancestor courses
    (`Admin/Dashboard/Index.cshtml.cs` uses `GetVisibleCoursesAsync`) — it is
    the admin's operational view; the admin is the one who hides courses.
  - SuperUser **global course list** hardcodes `Visibility = "Visible"` —
    visibility is per (org, course); a meaningful column needs a per-org
    display, which is a feature, not part of this bug.

## User Scenarios & Testing

### User Story 1 - A hidden course disappears from the child org's catalog (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a child org that inherits a root course, **When** an admin hides
   it for that org, **Then** the org's learners no longer see it in the
   catalog (full page and HTMX partial), and other courses are unaffected.
2. **Given** a hidden course, **When** the admin unhides it, **Then** it
   reappears.
3. **Given** a hidden course, **When** a learner of a DIFFERENT org (or the
   root) browses, **Then** they are unaffected.

**Independent Test**: `19-course-visibility.spec.ts` + full Playwright suite.
