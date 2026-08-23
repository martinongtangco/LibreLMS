# Tasks: Fix Course Creation OrganizationId

## Story 1 - Fix Razor Page Create Course

- [x] T1.1 Identify the bug: `Create.cshtml.cs` hardcodes `null` for `OrganizationId`
- [x] T1.2 Extract `OrganizationId` from user claims in `Create.cshtml.cs`
- [x] T1.3 Pass the `OrganizationId` to `CreateCourseRequest`

## Story 2 - Fix API Endpoint

- [x] T2.1 Identify the bug: `POST /api/courses` accepts `OrganizationId` from body, null → Guid.Empty
- [x] T2.2 Override null `OrganizationId` with user's claim value in `Program.cs`

## Verification

- [x] T3.1 Build passes: `dotnet build`
- [x] T3.2 App restarts and responds
- [x] T3.3 Create a course via UI — no duplicate key error
- [x] T3.4 Verify course has correct `OrganizationId` in DB
