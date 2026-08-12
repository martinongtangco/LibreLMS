# Research: Admin Courses Management Overhaul (Revised with SCORM Integration)

**Date**: 2025-08-11
**Feature**: specs/025-admin-courses-management

---

## Existing Decisions (from prior research)

### Decision: Use direct service injection instead of HTTP client for Create

**Rationale**: The existing `Create.cshtml.cs` uses `IHttpClientFactory` to POST to `/api/courses`. This pattern:
1. Does not carry auth cookies (server-to-server call), so the API endpoint returns 401/403
2. The API endpoint uses `[Authorize(Roles = "Admin")]` but `RoleNames` defines `SuperUser`, `OrgAdmin`, `Learner` — there is no `Admin` role
3. Other admin pages (`BulkEnroll.cshtml.cs`, `Upload.cshtml.cs`) inject `CourseCatalogService` directly

**Alternatives considered**:
- Fix the API endpoint's role to `SuperUser,OrgAdmin` and configure the HTTP client with auth cookies → adds complexity for no benefit
- Keep HTTP client pattern → would require cookie forwarding middleware

**Decision**: Rewrite `Create.cshtml.cs` to inject `CourseCatalogService` directly (matching the pattern used by other admin pages). Remove the dependency on the HTTP endpoint for course creation.

---

### Decision: Delete bug root cause — `GetAllCoursesAsync` returns empty org names and missing data

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

### Decision: Add `UpdateAsync` to `CourseCatalogService`

**Rationale**: The Catalog module has no update capability. `CourseCatalogService` has `CreateAsync`, `GetByIdAsync`, `ListAsync`, `BrowseAsync` but no `UpdateAsync`.

**Alternatives considered**:
- Add a PATCH endpoint to `/api/courses/{id}` → unnecessary since admin pages use direct service injection
- Add the update to `CourseVisibilityService` → wrong module boundary; updating a course is a Catalog concern

**Decision**: Add `UpdateAsync` method to `CourseCatalogService` in the Catalog module. The method signature will accept a course ID and an update request with the mutable fields (Title, ShortDescription, FullDescription, Category, Duration).

---

### Decision: Pagination approach — reuse `BrowseAsync` stored procedure pattern

**Rationale**: The public Courses page already implements search, category filter, and pagination using `BrowseAsync` stored procedure.

**Decision**: Reuse `BrowseAsync` from `CourseCatalogService` for the admin listing.

---

### Decision: Edit page as a new Razor Page (`Edit.cshtml`)

**Decision**: Create `Pages/Admin/Courses/Edit.cshtml` and `Edit.cshtml.cs` following the same pattern as Create.

---

### Decision: Table contrast fix — use `--color-surface` (#ffffff) for table wrapper background

**Decision**: Wrap the table in a `.card` div; add alternating rows.

---

### Decision: Default page size of 15 courses per page

**Decision**: 15 per page with previous/next pagination controls.

---

## New SCORM Integration Decisions

### Decision: Make `ScormPackage.CourseId` nullable with a filtered unique index

**Context**: Currently `ScormPackage.CourseId` is a non-nullable `Guid` with a unique index. This enforces 1:1 between Course and SCORM, and requires every SCORM package to belong to a course.

**Requirement**: SCORM packages must be able to exist without a course association (in an "available pool") and be associated later during course creation.

**Research**: In MSSQL, a standard `UNIQUE` index allows only one NULL value. To allow multiple NULL values while still enforcing uniqueness for non-NULL values, use a **filtered unique index**:
```sql
CREATE UNIQUE INDEX UX_ScormPackages_CourseId ON ScormPackages(CourseId) WHERE CourseId IS NOT NULL;
```

In EF Core, this is expressed as:
```csharp
entity.HasIndex(e => e.CourseId)
    .IsUnique()
    .HasFilter("[CourseId] IS NOT NULL");
```

**Decision**: 
1. Change `ScormPackage.CourseId` from `Guid` to `Guid?` (nullable)
2. Change the unique index to a filtered unique index: `WHERE CourseId IS NOT NULL`
3. Create an EF migration: `AddScormPackageNullableCourseId`
4. This allows:
   - Multiple SCORM packages with `CourseId = null` (available pool)
   - Each non-null `CourseId` to appear only once (1 SCORM per course)

**Alternatives considered**:
- Keep CourseId non-nullable and create a separate "SCORM library" table → adds unnecessary complexity; nullable is simpler
- Use a boolean `IsAssociated` flag instead of nullable CourseId → less explicit; nullable makes the intent clear

---

### Decision: Host orchestrates Course + SCORM creation via direct service injection

**Context**: Creating a course with SCORM requires:
1. Creating a Course in `CatalogDbContext` (Catalog module)
2. Uploading a SCORM package in `ScormDbContext` (Scorm module)
3. Setting the SCORM package's `CourseId` to the new course's ID

**Constraint**: Constitution Principle III — no cross-module references except through `*.Contracts`.

**Research**: The Host project already injects services from multiple modules directly. For example, `Upload.cshtml.cs` uses both `CourseCatalogService` and `CourseVisibilityService`. The Host is the orchestrator, not a module, so it can reference any module's services.

**Transaction concern**: Two DbContexts need to be updated atomically. Options:
1. Sequential saves (Course first, then SCORM) — risk: SCORM save fails, leaving an orphaned course with no SCORM
2. `System.Transactions.TransactionScope` — wraps both saves in a distributed transaction
3. Catalog save first, then SCORM save with CourseId — if SCORM fails, course exists without SCORM (acceptable — the user can add SCORM later)

**Decision**: Use sequential saves with option 3. Create the Course first, then upload SCORM. If SCORM upload fails:
- The course exists without SCORM (valid state — courses can exist without SCORM)
- Show an error message: "Course created but SCORM upload failed: {reason}"
- Redirect to the course edit page where the admin can retry the SCORM upload

This approach avoids the complexity of distributed transactions while maintaining data integrity — both the "course without SCORM" and "course with SCORM" states are valid.

**Alternatives considered**:
- `TransactionScope` → adds a dependency on `System.Transactions`, requires MSDTC for cross-database (overkill for same-server DBs)
- Create SCORM first with null CourseId, then create Course, then associate → more complex error handling

---

### Decision: Add `ListAvailableAsync` and `AssociateWithCourseAsync` to `ScormPackageService`

**Context**: The course creation form needs to list available (unassociated) SCORM packages for the "Associate existing SCORM" option.

**Decision**: Add two methods to `ScormPackageService`:
1. `ListAvailableAsync()` — returns all ScormPackages where `CourseId == null`, with ManifestTitle and Id for the dropdown
2. `AssociateWithCourseAsync(packageId, courseId)` — sets the `CourseId` of an available package to the given course

**Alternatives considered**:
- Add these to a new service → unnecessary; ScormPackageService already owns SCORM package operations
- Expose through an API endpoint → unnecessary; Host pages use direct service injection

---

### Decision: Add `ReplacePackageAsync` to `ScormPackageService` for SCORM replacement

**Context**: When editing a course that already has SCORM, the admin may want to replace the SCORM content with a new upload.

**Decision**: Add `ReplacePackageAsync(courseId, zipStream)` to `ScormPackageService`:
1. Find the existing ScormPackage for the course
2. Delete its content directory from the filesystem
3. Delete the ScormPackage entity
4. Upload the new SCORM package (reusing existing `UploadAsync` logic)

**Alternatives considered**:
- Keep old packages with a `IsReplaced` flag → accumulates stale content on disk
- Version the packages → adds complexity the feature doesn't need

---

### Decision: Modify the SCORM upload endpoint to accept nullable courseId

**Context**: The existing `/api/scorm/upload` endpoint requires a `courseId`. The Admin/Upload page must be updated to upload SCORM packages to the "available pool" without a course.

**Decision**: 
1. Change the upload endpoint to accept an optional `courseId` parameter
2. When `courseId` is not provided, create the ScormPackage with `CourseId = null`
3. The Admin/Upload page removes the course dropdown and uploads directly to the available pool

**Alternatives considered**:
- Create a new `/api/scorm/upload-available` endpoint → unnecessary; optional parameter is cleaner

---

### Decision: Block SCORM launch when CourseId is null

**Context**: SCORM packages in the available pool should not be directly launchable. They need a course association to be consumed.

**Research**: The SCORM launch flow is:
1. User navigates to `/Scorm/Launch?courseId={id}`
2. `ScormLaunchModel` calls `/api/scorm/{courseId}/launch`
3. The endpoint finds the ScormPackage by `CourseId` and returns launch info

**Decision**: The existing launch flow already queries by `CourseId`. A package with `CourseId = null` will simply not be found by `GetPackageByCourseIdAsync(courseId)`. No code change needed — the null CourseId naturally prevents launch.

---

### Decision: Delete course with SCORM requires confirmation warning

**Context**: When a course with SCORM content is deleted, the associated ScormPackage and its filesystem content directory should also be deleted.

**Decision**: Show a confirmation dialog that warns the admin: "This course has SCORM content. Deleting this course will also permanently delete the SCORM package and its extracted files. Are you sure?"

**Alternatives considered**:
- Delete SCORM silently → risky; admin may not realize content is being lost
- Orphan the SCORM (set CourseId to null) → creates stale data; user confirmed they want full deletion

---

### Decision: Admin/Upload page focuses on SCORM pool management only

**Context**: The existing `/Admin/Upload` page requires course selection. With SCORM integration into course creation, this page needs a new purpose.

**Decision**: Repurpose the Admin/Upload page to:
1. Upload SCORM ZIP files to the available pool (no course selection)
2. List available (unassociated) SCORM packages
3. Allow deleting available packages from the pool

Association with courses is done exclusively through the Courses pages (create/edit). This avoids duplication of the association workflow.

---

### Decision: 50MB upload size limit

**Decision**: Set `MaxRequestSize` to 50MB (52_428_800 bytes) for SCORM upload endpoints.

---

### Decision: Single-SCO launch (multi-SCO out of scope)

**Context**: SCORM manifests can define multiple SCOs (Sharable Content Objects) with sequencing. The constitution explicitly excludes SCORM 2004 and multi-SCO sequencing.

**Decision**: Continue using the first SCO from the manifest as the launch point. Most SCORM 1.2 packages from common authoring tools have a single SCO. Multi-SCO would require:
- A sequencing engine (ADL SCORM Run-Time)
- Navigation between SCOs
- Aggregate scoring across SCOs
- Persistent state for SCO completion order

This is a feature slice on its own, not a course management concern. If needed later, it can be added via `/speckit.specify`.

**Alternatives considered**:
- Full multi-SCO support → out of scope per constitution
- Let authoring tools define organization → adds manifest parser complexity for little current benefit

---

### Decision: Course creation form uses radio buttons for SCORM mode

**Context**: The course creation form needs three mutually exclusive SCORM options.

**Decision**: Use a radio button group with three options:
1. "No SCORM content" (default) — creates course without SCORM
2. "Upload new SCORM package" — reveals a file input for ZIP upload
3. "Associate existing SCORM" — reveals a dropdown of available packages

JavaScript (minimal) toggles visibility of the file input and dropdown based on radio selection. This keeps the form simple and avoids multi-step wizards.

**Alternatives considered**:
- Multi-step wizard (step 1: course details, step 2: SCORM) → adds complexity, requires state management
- Separate tabs → overkill for three simple options

---

### Decision: `CreateCourseRequest` gains optional `ScormPackageId` for association flow

**Context**: When the admin chooses "Associate existing SCORM", the course creation handler needs to know which SCORM package to link.

**Decision**: Add `Guid? ScormPackageId` to `CreateCourseRequest`. When this is set:
1. Create the course as normal
2. Call `ScormPackageService.AssociateWithCourseAsync(scormPackageId, courseId)`

When null, the course is created without SCORM association.

**Alternatives considered**:
- Pass ScormPackageId as a separate parameter to CreateAsync → pollutes the method signature; DTO is cleaner
- Create a new `CreateCourseWithScormRequest` → unnecessary; optional field handles both cases

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
| **SCORM CourseId** | Non-nullable with unique index; requires course for every upload | Make nullable with filtered unique index |
| **SCORM association** | No way to list or associate unassociated packages | Add `ListAvailableAsync` and `AssociateWithCourseAsync` |
| **SCORM replacement** | No way to replace SCORM on existing course | Add `ReplacePackageAsync` |
| **Upload page** | Requires course selection; can't upload to pool | Make courseId optional in upload endpoint |
| **Transaction** | Two DbContexts need coordination | Sequential saves; course created without SCORM if upload fails |
| **Launch safety** | SCORM without course can't be found by courseId | Natural safety — no code change needed |
