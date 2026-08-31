# Plan: Enforce Hide-Course Visibility in the Learner Catalog

**Input**: [spec.md](spec.md)

## Summary

Filter `!IsHidden` at the two learner-facing read spots in
`src/Host/Pages/Courses/Index.cshtml.cs`, plus a new E2E spec that builds a
child org + learner per run to exercise the inheritance path.

## Technical Approach

- **`src/Host/Pages/Courses/Index.cshtml.cs`** (the only production file
  touched):
  - `GetPagedCourses`: `var visibleCourseIds = visible.Where(v => !v.IsHidden)
    .Select(v => v.CourseId).ToHashSet();`
  - `GetCategoriesAsync`: `visible.Where(v => !v.IsHidden).ToDictionary(...)`
  - Update the two comments to say hidden courses are excluded (spec 009
    scenario 5 / bug-047).
- **`tests/Playwright.Tests/tests/19-course-visibility.spec.ts`** (new):
  - Serial (single test). Setup via `page.request` as SuperUser
    (`authFixture.loginAs(page, 'SuperUser')` — the fixture's context cookies
    apply to `page.request`):
    - `POST /api/organizations` `{ name, description, parentId: <root org
      00000000-0000-0000-0000-000000000001> }` → childOrgId.
    - `POST /api/users` `{ name, email: visib<ts>@example.com, password
      (policy-passing, cf. 14-profile-courses 'Sup3rSecret!x9' pattern),
      role: 'Learner', organizationId: childOrgId }` → learnerId (admin-created
      accounts are verified — no outbox step).
  - Resolve the target course id from `GET /api/courses` (a seeded
    non-SCORM root course, e.g. title 'Database Design Fundamentals' — do not
    hardcode the GUID).
  - Learner page (fresh context via `loginAs` on a new page/context):
    1. `/Courses/Index` → target course card visible (inherited).
    2. SuperUser `PUT /api/admin/courses/{courseId}/visibility?organizationId={childOrgId}&isHidden=true&createdBy={superUserId}` → 200.
    3. Learner `/Courses/Index` → target card gone; a second seeded course
       still visible.
    4. Unhide (`isHidden=false`) → target card back.
  - `finally`: `DELETE /api/users/{learnerId}` then
    `DELETE /api/organizations/{childOrgId}` (idempotent across runs via
    unique names; leftover inert override row is acceptable — no FK).
  - No `test.describe.configure({ mode: 'serial' })` needed (one test) but
    keep the file independent of alice's state (it only READS root
    courses, never mutates them).
- **No changes** to the Management module, contracts, endpoints, or the
  dashboard/admin-list surfaces (documented out-of-scope in spec).
  **ArchitectureTests** unaffected.

## Verification (Principle XIII)

1. Rebuild in the devcontainer + restart the app in-container
   (ASPNETCORE_ENVIRONMENT=Development, Database=LearningLms, Valkey) — show
   build output + "Now listening" + 200.
2. `dotnet test tests/ArchitectureTests` (module boundaries intact) — and
   `dotnet test tests/Scorm.Tests` needs no re-run (untouched) but
   ArchitectureTests is the compile/boundary gate.
3. Isolated `19-course-visibility.spec.ts` green — AND red-verified: run it
   against the pre-fix code (temporarily revert the two filters) to prove it
   catches the bug, then restore.
4. FULL Playwright suite green (gate 2); re-run after merge (gate 3).
