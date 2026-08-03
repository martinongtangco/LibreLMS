# Data Model: Organic Design System Redesign

**Feature**: 017-organic-ui-redesign
**Date**: 2026-08-03

## Domain Model Changes

### Student (existing — modified)

**File**: `src/Modules/Enrollment/Domain/Student.cs`

New properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EmailNotificationsEnabled` | `bool` | `true` | Backs the Settings page's "Email notifications" toggle. |
| `ThemePreference` | `string` | `"System"` | Backs the Settings page's "Theme" row. Stored/displayed only — no dark-theme tokens exist yet, so non-default values do not change rendered appearance this slice (see research.md §4). |

No changes to existing properties. Full entity after this change:

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Primary key (existing) |
| `Name` | `string` | (existing) |
| `Email` | `string` | (existing) |
| `PasswordHash` | `string` | (existing) |
| `Roles` | `string` | Comma-separated role names (existing) |
| `OrganizationId` | `Guid` | (existing) |
| `CreatedAt` | `DateTimeOffset` | (existing) |
| `EmailNotificationsEnabled` | `bool` | **NEW** |
| `ThemePreference` | `string` | **NEW** |

**Migration**: One new EF Core migration under `src/Host/Migrations/` adding both columns with the defaults above (`NOT NULL DEFAULT`), so existing seeded/production rows get valid values with no backfill script required.

No other entities (`Course`, `Enrollment`, `CourseAttempt`, `Organization`) change shape — this feature only changes how their existing data is *presented*.

## New Read Query

### `EnrollmentService.GetEnrollmentCountsByCourseAsync`

**File**: `src/Modules/Enrollment/Application/EnrollmentService.cs`

```
Task<IReadOnlyDictionary<Guid, int>> GetEnrollmentCountsByCourseAsync(IEnumerable<Guid> courseIds)
```

- Input: the set of course ids the caller (Admin Dashboard) already has visibility into.
- Behavior: one `GROUP BY CourseId` query over `Enrollments` filtered to the given ids; courses with zero enrollments are simply absent from the returned dictionary (caller defaults to 0).
- No new contract/DTO — this is an `Enrollment`-module-internal Application method called directly by `Host` (same pattern as `GetMyEnrollmentsAsync`), not a cross-module `*.Contracts` addition (see research.md §5).

### `EnrollmentService.GetPreferencesAsync` / `UpdatePreferencesAsync`

```
Task<(bool EmailNotificationsEnabled, string ThemePreference)> GetPreferencesAsync(Guid studentId)
Task UpdatePreferencesAsync(Guid studentId, bool emailNotificationsEnabled, string themePreference)
```

- Read/write the two new `Student` columns for the Settings page. No new entity — thin wrappers over `EnrollmentDbContext.Students`.

## View-Model Shapes (presentation only, no persistence)

These are Razor Page view-model records assembled from existing/new query results — not new domain concepts.

### Course card (My Courses / Browse Courses)

| Field | Source |
|-------|--------|
| `Category` | `Course.Category` |
| `Title` | `Course.Title` |
| `Description` | `Course.ShortDescription` |
| `Hours` | `Course.Duration` (already a display string, e.g. "3 hours") |
| `StatusLabel` | `ScormHelpers.GetDisplayLabel(LatestStatus)` (My Courses only) |
| `StatusTagClass` | `tag-neutral` if no attempt/"Not Started", else `tag-accent-2` |
| `ProgressPercent` | `LatestScore ?? (completed/passed ? 100 : 0)` (My Courses only) |
| `EnrolledDate` | `Enrollment.EnrolledAt` (My Courses only) |
| `IsEnrolled` | Existing enrollment lookup (Browse Courses "✓ Enrolled" tag) |

### Dashboard stat tile

| Field | Source |
|-------|--------|
| `Label` | Existing metric label (Organizations/Learners/Courses/Enrollments/Completion Rate) |
| `Value` | Existing `IndexModel` properties (`TotalOrganizations`, `TotalLearners`, `TotalCourses`, `TotalEnrollments`, `CompletionRate`) — unchanged computation |

### Dashboard course row

| Field | Source |
|-------|--------|
| `Title`, `Category`, `Hours` | `Course` (via existing `CourseVisibilityService.GetAllCoursesAsync`) |
| `EnrollmentCount` | New `GetEnrollmentCountsByCourseAsync` result, defaulting to 0 |

### Profile page

| Field | Source |
|-------|--------|
| `Name`, `Email` | `ClaimsPrincipal` (`ClaimTypes.Name`/`ClaimTypes.Email`), same claims already set at login |
| `RoleLabel` | Existing role claim(s), formatted the same way current admin-only nav gating reads them |

### Settings page

| Field | Source |
|-------|--------|
| `EmailNotificationsEnabled`, `ThemePreference` | New `GetPreferencesAsync` |

## State Transitions

None — no new entity lifecycle. `Student.EmailNotificationsEnabled`/`ThemePreference` are simple mutable fields updated in place by `UpdatePreferencesAsync`; there is no workflow/status machine involved.
