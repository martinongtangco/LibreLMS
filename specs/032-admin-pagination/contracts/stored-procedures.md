# Contract: Stored Procedures (Admin List Pagination)

**Feature**: [../spec.md](../spec.md) | **Database**: single MSSQL database, `dbo` schema

All procedures: `SET NOCOUNT ON`; parameters default safely; result set 1 = page rows,
result set 2 = single-row `COUNT(*) AS TotalCount` over the *filtered* set. Invalid
`@PageSize` (≤ 0) → 10; invalid `@PageNumber` (≤ 0) → 1. Empty strings are treated as NULL.
No dynamic SQL — sort keys selected via a static `CASE` expression.

---

## 1. `dbo.BrowseCourses` — EXTENDED (Catalog context migration)

Replaces the procedure created by migration `20260805020000_AddFullTextIndexAndBrowseProcedure`
(drop + recreate in the new migration, same idempotent-guard pattern).

| Parameter | Type | Default | Meaning |
|---|---|---|---|
| `@SearchTerm` | NVARCHAR(200) | NULL | case-insensitive `LIKE '%term%'` on `Courses.Title`; NULL/`''` = no filter |
| `@Category` | NVARCHAR(100) | NULL | exact match on `Courses.Category`; NULL/`''` = no filter |
| `@PageSize` | INT | 12 → **10** | rows per page (floor 10 when ≤ 0) |
| `@PageNumber` | INT | 1 | 1-based page (floor 1 when ≤ 0) |
| `@SortBy` | NVARCHAR(20) | `'title'` | `'title'` \| `'category'` \| `'duration'`; anything else → `'title'` |
| `@SortDirection` | NVARCHAR(4) | `'asc'` | `'asc'` \| `'desc'`; anything else → `'asc'` |

**Result set 1** (ordered as specified, PK tie-break `c.Id ASC` last):

| # | Column | Type |
|---|---|---|
| 0 | Id | uniqueidentifier |
| 1 | Title | nvarchar |
| 2 | ShortDescription | nvarchar |
| 3 | Category | nvarchar |
| 4 | Duration | nvarchar |
| 5 | **OrganizationId** *(added)* | uniqueidentifier |

**Result set 2**: `TotalCount` (int) — count of rows matching the search+category filter.

**Compatibility**: calling with the original four parameters (or omitting the new two)
reproduces the legacy behavior exactly (title ASC, no OrganizationId consumer change needed —
the column is appended, existing reader indexes 0..4 unchanged). Public Browse Courses
behavior is unchanged (FR-017).

**ORDER BY** (static, parameter-driven — exactly one branch is non-NULL per row; all other
branches evaluate to NULL for every row and therefore contribute nothing to the order):

```sql
ORDER BY
    CASE WHEN @SortBy = 'title'    AND @SortDirection = 'asc'  THEN c.Title    END ASC,
    CASE WHEN @SortBy = 'title'    AND @SortDirection = 'desc' THEN c.Title    END DESC,
    CASE WHEN @SortBy = 'category' AND @SortDirection = 'asc'  THEN c.Category END ASC,
    CASE WHEN @SortBy = 'category' AND @SortDirection = 'desc' THEN c.Category END DESC,
    CASE WHEN @SortBy = 'duration' AND @SortDirection = 'asc'  THEN c.Duration END ASC,
    CASE WHEN @SortBy = 'duration' AND @SortDirection = 'desc' THEN c.Duration END DESC,
    c.Id ASC
```

The `@SortBy`/`@SortDirection` values are normalized to the allowed set at the top of the
procedure (`ISNULL`/`CASE` to defaults), so the `CASE` branches above always match at most
one column-direction pair. Behavior is pinned by the `Catalog.Tests` cases in research.md R8.

---

## 2. `dbo.AdminListEnrollments` — NEW (Enrollment context migration)

Cross-module join (`Courses`) documented in `docs/adr/0008-cross-module-sql-join-for-admin-listing.md`.

| Parameter | Type | Default | Meaning |
|---|---|---|---|
| `@StudentName` | NVARCHAR(200) | NULL | case-insensitive `LIKE '%term%'` on `Students.Name`; NULL/`''` = no filter |
| `@CourseTitle` | NVARCHAR(200) | NULL | case-insensitive `LIKE '%term%'` on `Courses.Title`; NULL/`''` = no filter |
| `@PageSize` | INT | 10 | floor 10 when ≤ 0 |
| `@PageNumber` | INT | 1 | floor 1 when ≤ 0 |

**Join**: `Enrollments e INNER JOIN Students s ON e.StudentId = s.Id INNER JOIN Courses c ON
e.CourseId = c.Id` (inner joins preserve today's contract behavior: rows whose course no
longer exists are omitted — see `IEnrollmentAdmin.ListAsync` doc).

**Result set 1** (ordered `e.EnrolledAt DESC, e.Id DESC`):

| # | Column | Type |
|---|---|---|
| 0 | EnrollmentId (e.Id) | uniqueidentifier |
| 1 | StudentId (s.Id) | uniqueidentifier |
| 2 | StudentName (s.Name) | nvarchar |
| 3 | StudentEmail (s.Email) | nvarchar |
| 4 | CourseId (c.Id) | uniqueidentifier |
| 5 | CourseTitle (c.Title) | nvarchar |
| 6 | OrganizationId (s.OrganizationId) | uniqueidentifier |
| 7 | EnrolledAt (e.EnrolledAt) | datetimeoffset |

**Result set 2**: `TotalCount` (int) — filtered count.

---

## 3. `dbo.AdminListLearners` — NEW (Enrollment context migration)

| Parameter | Type | Default | Meaning |
|---|---|---|---|
| `@Search` | NVARCHAR(200) | NULL | case-insensitive `LIKE '%term%'` on `Students.Name` **OR** `Students.Email`; NULL/`''` = no filter |
| `@Role` | NVARCHAR(50) | NULL | exact match on `Students.Roles`; NULL/`''` = no filter |
| `@PageSize` | INT | 10 | floor 10 when ≤ 0 |
| `@PageNumber` | INT | 1 | floor 1 when ≤ 0 |

**Result set 1** (ordered `s.Name ASC, s.Id ASC`):

| # | Column | Type |
|---|---|---|
| 0 | Id (s.Id) | uniqueidentifier |
| 1 | Name (s.Name) | nvarchar |
| 2 | Email (s.Email) | nvarchar |
| 3 | Roles (s.Roles) | nvarchar |
| 4 | OrganizationId (s.OrganizationId) | uniqueidentifier |
| 5 | CreatedAt (s.CreatedAt) | datetimeoffset |
| 6 | IsEmailVerified (s.IsEmailVerified) | bit |
| 7 | AvatarPath (s.AvatarPath) | nvarchar (nullable) |

(columns map 1:1 onto `StudentProvisionedDto`, whose `PasswordHash`/`SecurityStamp` are
deliberately never exposed by the listing — consistent with the existing DTO's credential
exclusion)

**Result set 2**: `TotalCount` (int) — filtered count.
