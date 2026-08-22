# Contract: Module Interface Changes (Admin List Pagination)

**Feature**: [../spec.md](../spec.md)

All changes are **additive** — existing method signatures, records, and their consumers
(dashboard, my-courses, public browse) are untouched. Module-boundary rule (Constitution III):
new surface lives only in `*.Contracts`; implementations stay in the owning module.

## Enrollment.Contracts

### `IEnrollmentAdmin` (implemented by `EnrollmentAdminService`)

```csharp
/// <summary>Paged admin listing, newest-first. Filters are case-insensitive contains on
/// student name and course title. Enrollments whose course no longer exists are omitted
/// (same semantics as ListAsync). Returns only the requested page plus the filtered total.</summary>
Task<AdminEnrollmentPageResult> ListPagedAsync(
    string? studentName, string? courseTitle, int pageNumber, int pageSize);
```

New records:

```csharp
/// <summary>One enrollment row as returned by the AdminListEnrollments procedure
/// (includes learner name/email and the learner's org id for display enrichment).</summary>
public record AdminEnrollmentRow(
    Guid EnrollmentId, Guid StudentId, string StudentName, string StudentEmail,
    Guid CourseId, string CourseTitle, Guid OrganizationId, DateTimeOffset EnrolledAt);

public record AdminEnrollmentPageResult(IList<AdminEnrollmentRow> Items, int TotalCount);
```

### `IUserProvisioning` (implemented by `UserProvisioningService`)

```csharp
/// <summary>Paged admin listing, name-ascending. Search is case-insensitive contains on
/// name OR email; roleFilter is an exact role string. Returns only the requested page plus
/// the filtered total.</summary>
Task<StudentPageResult> ListPagedAsync(
    string? search, string? roleFilter, int pageNumber, int pageSize);
```

New record:

```csharp
public record StudentPageResult(IList<StudentProvisionedDto> Items, int TotalCount);
```

(`StudentProvisionedDto` is unchanged — it already carries `OrganizationId`.)

### `IUserLookup` (implemented by `UserLookupService`)

```csharp
// before
public record UserSummary(Guid Id, string Name, string Email);
// after (additive last parameter)
public record UserSummary(Guid Id, string Name, string Email, Guid OrganizationId);
```

Single implementor and a small number of consumers (batch display lookups); the added field
lets the Management module resolve org names for enrollment rows in one batch instead of the
current per-row `GetUserScopeAsync` call.

## Catalog module (application-level, not a cross-module contract)

`CourseCatalogService.BrowseAsync` gains two optional trailing parameters (existing call
sites compile unchanged):

```csharp
Task<BrowseResult> BrowseAsync(
    string? searchTerm, string? category, int pageNumber, int pageSize,
    HashSet<Guid>? visibleCourseIds = null,
    string sortBy = "title",            // "title" | "category" | "duration"
    string sortDirection = "asc");      // "asc" | "desc"
```

`CourseItemDto` gains `Guid OrganizationId` as a trailing record parameter.
`BrowseResult` is unchanged (`Items`, `TotalCount`, `PageNumber`, `PageSize`).

## Management module (admin facade — new delegating variants)

```csharp
// AdminEnrollmentService
/// <summary>Paged variant of ListAllEnrollmentsAsync: delegates to
/// IEnrollmentAdmin.ListPagedAsync, then enriches org names for the page's distinct
/// OrganizationIds via IOrganizationLookup (page-local cache). Old method retained.</summary>
Task<EnrollmentPageResult> ListAllEnrollmentsPagedAsync(
    string? studentName, string? courseTitle, int pageNumber, int pageSize);

// new record alongside existing EnrollmentDto
public record EnrollmentPageResult(IList<EnrollmentDto> Items, int TotalCount);

// UserService
/// <summary>Paged variant of ListAllAsync: delegates to IUserProvisioning.ListPagedAsync,
/// then enriches org names for the page's distinct OrganizationIds. Old method retained.</summary>
Task<UserPageResult> ListAllPagedAsync(
    string? search, string? roleFilter, int pageNumber, int pageSize);

// new record alongside existing UserDto
public record UserPageResult(IList<UserDto> Items, int TotalCount);
```

## Host (page-model surface — query contract)

See [admin-pages-query.md](admin-pages-query.md).
