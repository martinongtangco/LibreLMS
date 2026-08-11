# Research: Admin Courses Management Overhaul

**Date**: 2025-08-11
**Feature**: specs/025-admin-courses-management

## Decision: Use direct service injection instead of HTTP client for Create

**Rationale**: The existing `Create.cshtml.cs` uses `IHttpClientFactory` to POST to `/api/courses`. This pattern:
1. Does not carry auth cookies (server-to-server call), so the API endpoint returns 401/403
2. The API endpoint uses `[Authorize(Roles = "Admin")]` but `RoleNames` defines `SuperUser`, `OrgAdmin`, `Learner` — there is no `Admin` role
3. Other admin pages (`BulkEnroll.cshtml.cs`, `Upload.cshtml.cs`) inject `CourseCatalogService` directly

**Alternatives considered**:
- Fix the API endpoint's role to `SuperUser,OrgAdmin` and configure the HTTP client with auth cookies → adds complexity for no benefit
- Keep HTTP client pattern → would require cookie forwarding middleware

**Decision**: Rewrite `Create.cshtml.cs` to inject `CourseCatalogService` directly (matching the pattern used by other admin pages). Remove the dependency on the HTTP endpoint for course creation.

---

## Decision: Delete bug root cause — `GetAllCoursesAsync` returns empty org names and missing data

**Rationale**: Investigated `CourseVisibilityService.DeleteCourseAsync`:
- The method correctly removes from both `managementCtx` and `catalogCtx` and calls `SaveChangesAsync` on both
- The Razor page handler `OnPostDeleteAsync` correctly calls the service and catches `KeyNotFoundException`

**However**, `GetAllCoursesAsync` has issues:
- It hardcodes `orgName = "Unknown"` without calling `orgLookup.GetOrganizationAsync` — meaning org names are never resolved
- The `IsInherited` and `IsHidden` flags are hardcoded to `false` (correct for a super-admin view but misleading)

The delete handler likely fails silently because:
1. `OnGetAsync` after delete may throw if `GetAllCoursesAsync` has issues with org name resolution
2. Or the success message is shown but the page reload fails and shows the error instead

**Decision**: Fix `GetAllCoursesAsync` to properly resolve organization names. The delete method itself is structurally correct but needs to be tested after the org name fix.

---

## Decision: Add `UpdateAsync` to `CourseCatalogService`

**Rationale**: The Catalog module has no update capability. `CourseCatalogService` has `CreateAsync`, `GetByIdAsync`, `ListAsync`, `BrowseAsync` but no `UpdateAsync`.

**Alternatives considered**:
- Add a PATCH endpoint to `/api/courses/{id}` → unnecessary since admin pages use direct service injection
- Add the update to `CourseVisibilityService` → wrong module boundary; updating a course is a Catalog concern

**Decision**: Add `UpdateAsync` method to `CourseCatalogService` in the Catalog module. The method signature will accept a course ID and an update request with the mutable fields (Title, ShortDescription, FullDescription, Category, Duration).

---

## Decision: Pagination approach — reuse `BrowseAsync` stored procedure pattern

**Rationale**: The public Courses page (`Pages/Courses/Index.cshtml.cs`) already implements search, category filter, and pagination using:
1. `CourseCatalogService.BrowseAsync()` which calls a T-SQL stored procedure `BrowseCourses`
2. A `BrowseResult` record with `Items`, `TotalCount`, `PageNumber`, `PageSize`
3. A reusable `_Pagination.cshtml` partial

**Alternatives considered**:
- Use LINQ `.Skip().Take()` in-memory → loads all courses into memory first, bad for performance
- Build a new stored procedure for admin-only queries → unnecessary duplication

**Decision**: Reuse `BrowseAsync` from `CourseCatalogService` for the admin listing. The admin page will use the same stored procedure, which already supports search by title and category filter with server-side pagination. This is the pattern the app already uses.

---

## Decision: Edit page as a new Razor Page (`Edit.cshtml`)

**Rationale**: No edit page exists. The app uses Razor Pages (not Blazor). The pattern is consistent: each CRUD operation has its own page.

**Alternatives considered**:
- Inline edit in the table (HTMX modal) → more complex, requires partial view for the form
- Combine Create and Edit into one page with a mode parameter → adds branching logic

**Decision**: Create `Pages/Admin/Courses/Edit.cshtml` and `Edit.cshtml.cs` following the same pattern as Create. Inject `CourseCatalogService` directly.

---

## Decision: Table contrast fix — use `--color-surface` (#ffffff) for table wrapper background

**Rationale**: Current issue:
- Page background: `--page-bg: #f5ead8` (warm beige)
- Table header: `--color-bg: #faf8f4` (near-white)
- These two colors have very low contrast (~1.05:1)

The `.card` class uses `--color-surface: #ffffff` with a `--shadow-card` and border. Wrapping the table in a `.card` will create clear visual separation.

**Alternatives considered**:
- Darken the table header → would create a new color not in the design system
- Add a border around the table → insufficient; the header still blends in

**Decision**: Wrap the table in a `.card` div (which uses `--color-surface: #ffffff`) to create contrast against `--page-bg`. Add `tr:nth-child(even)` styling for alternating row colors. The `.data-table` class already has border-bottom on cells, which provides row separation.

---

## Decision: Default page size of 15 courses per page

**Rationale**: The public courses page uses 12 per page. Admin tables typically show more rows. 15 is a reasonable default that fits well on desktop and is manageable on mobile.

**Alternatives considered**:
- 10 per page → too few for admin workflows
- 20 per page → may require excessive scrolling on mobile

**Decision**: 15 per page with previous/next pagination controls (reusing `_Pagination.cshtml` pattern adapted for the admin table).

---

## Technical Findings Summary

| Area | Finding | Action |
|------|---------|--------|
| Create flow | Uses HTTP client; API has wrong role name | Rewrite to use direct service injection |
| Edit | No page or service method exists | Create Edit page + UpdateAsync method |
| Delete | `GetAllCoursesAsync` doesn't resolve org names | Fix org name resolution in GetAllCoursesAsync |
| Pagination | `BrowseAsync` SP exists and supports search/category | Reuse for admin page |
| Table contrast | Header (#faf8f4) blends with page (#f5ead8) | Wrap table in `.card` div; add alternating rows |
| Authorization | Razor page uses `SuperUser,OrgAdmin`; API uses `Admin` | Admin pages use direct injection, no API call needed |
| Course fields | Title, ShortDesc, FullDesc, Category, Duration, OrgId | All fields editable except OrgId and CreatedAt |
