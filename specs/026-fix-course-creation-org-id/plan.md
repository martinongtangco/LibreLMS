# Implementation Plan: Fix Course Creation OrganizationId

## Changes Required

### 1. Fix `Create.cshtml.cs` (Razor Page)
- Extract `OrganizationId` from `User.FindFirstValue(OrgClaimTypes.OrganizationId)` 
- Parse to `Guid?` and pass it to `CreateCourseRequest` instead of `null`
- File: `src/Host/Pages/Admin/Courses/Create.cshtml.cs`

### 2. Fix `Program.cs` (API Endpoint)
- The `POST /api/courses` endpoint should use the authenticated user's `OrganizationId` from claims when the request body has `null`
- Override `request.OrganizationId` with the claim value if null
- File: `src/Host/Program.cs` line ~155

## Verification
- Build succeeds: `dotnet build`
- App restarts and responds to HTTP
- Create a course via the admin UI and confirm no duplicate key error
- Verify the course has the correct `OrganizationId` in the database
