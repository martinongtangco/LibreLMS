# Bug Fix Specification: Course Creation Fails with Duplicate Key Error

**Feature Branch**: `bug/026-fix-course-creation-org-id`

**Created**: 2026-08-12

**Status**: Complete (merged 2026-08-12)

**Input**: User report: "i found a bug in the course creation, there's an error. try testing it by adding a title 'some course', description 'some description' full description 'full description', category 'catg', Duration 1, and No scorm content"

## Root Cause

The `Create.cshtml.cs` Razor Page hardcodes `OrganizationId` to `null` when creating a `CreateCourseRequest`:

```csharp
new CreateCourseRequest(Title, ShortDescription, FullDescription, Category, Duration, null)
```

`CourseCatalogService.CreateAsync` converts `null` to `Guid.Empty`, so every course created via the admin UI gets `OrganizationId = 00000000-0000-0000-0000-000000000000`. Since the `Courses` table has a unique index on `(Title, OrganizationId)`, any duplicate title fails with a SQL duplicate key error.

The same issue exists in the `POST /api/courses` minimal API endpoint (Program.cs line 155), which accepts `OrganizationId` from the request body — if the client sends `null`, the same `Guid.Empty` fallback applies.

## User Scenarios & Testing

### User Story 1 - Create Course with Correct Organization Assignment (Priority: P1)

As an admin (SuperUser or OrgAdmin), I want courses I create to be assigned to my organization, so that they are properly scoped and don't cause duplicate key errors.

**Independent Test**: Log in as an admin, navigate to Admin/Courses/Create, fill in course details, submit, and confirm the course is created with the admin's `OrganizationId` from their auth claim.

**Acceptance Scenarios**:

1. **Given** an admin is on the course creation form, **When** they fill in all required fields and submit, **Then** the course is created with the admin's `OrganizationId` (from `OrgClaimTypes.OrganizationId` claim) and they see a success message
2. **Given** a course was just created, **When** the admin views the course in the database, **Then** `OrganizationId` is NOT `Guid.Empty`
3. **Given** two admins from different organizations create courses with the same title, **Then** both courses are created successfully because they have different `OrganizationId` values

### User Story 2 - API Endpoint Uses Authenticated User's Organization (Priority: P1)

As an API caller with admin credentials, I want course creation via the API to use my authenticated organization context, so that I don't need to send `OrganizationId` in the request body.

**Acceptance Scenarios**:

1. **Given** an authenticated admin calls `POST /api/courses`, **When** the request body has `null` or no `OrganizationId`, **Then** the course is created using the admin's `OrganizationId` from their auth claim

## Implementation Notes

- The user's `OrganizationId` is available from `User.FindFirstValue(OrgClaimTypes.OrganizationId)` in the Razor Page (via `HttpContext.User`) and from `HttpContext.User.FindFirstValue(OrgClaimTypes.OrganizationId)` in the minimal API endpoint.
- Two files need changes: `Create.cshtml.cs` and `Program.cs`.
- `Edit.cshtml.cs` is not affected (uses `UpdateAsync` which doesn't touch `OrganizationId`).
