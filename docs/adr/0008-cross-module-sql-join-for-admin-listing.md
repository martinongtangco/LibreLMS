# ADR 0008: Cross-module SQL join for the Admin Enrollments list

**Status:** Accepted (spec 032, 2026-07-21)

## Context

The Admin > Enrollments page needed server-side filtering by *course title* plus
server-side paging (spec 032, FR-008/FR-013). The `Enrollments` table is owned by
the Enrollment module; the `Courses` table (and the course title) is owned by the
Catalog module. The filtering predicate (`CourseTitle LIKE @Term`) therefore had
to be applied to a Catalog-owned column before `OFFSET/FETCH` ran — otherwise
paging would be applied to the wrong result set and page boundaries would shift
as titles changed.

The whole system runs on one shared MSSQL database (ADR 0003 polyglot storage),
so a T-SQL `JOIN` across the two tables is always possible at the storage layer.
The question is whether it is acceptable at the *module* layer: the Enrollment
module's stored procedure would read a table it does not own.

## Decision

`AdminListEnrollments` (Enrollment module) joins `Courses` in T-SQL to expose
`CourseTitle` as both a filter and a result column, and uses `LEFT JOIN` so
enrollments referencing a deleted course are not silently dropped from the list.
The join is the *only* cross-module storage access in the codebase.

The C# module boundary is unchanged: the Enrollment module's contract
(`IEnrollmentAdmin.ListPagedAsync`) never returns a Catalog domain type, the
join happens entirely inside the procedure, and no Catalog-owned *C#* code is
touched.

Note the symmetric precedent: the Catalog module's `BrowseCourses` procedure
already joins the Enrollment-owned `Enrollments` table to expose the
`IsEnrolled` flag, so a join in this direction is not new for the codebase —
what is new is that the join now carries a *filter predicate*, which is why it
needed an ADR. The Management layer (`AdminEnrollmentService`) composes the result
with a page-local organization-name cache, which is an existing pattern
(`EnrollmentAdminService`).

## Consequences

- **Positive:** the course-title filter and paging happen in one round trip;
  page totals and boundaries are correct by construction (verified by the
  `AdminListEnrollments` integration tests, including the orphan-row case).
- **Positive:** `LEFT JOIN` keeps deleted-course enrollments visible with a
  NULL title, which is the only useful behaviour for an audit-style admin list.
- **Negative:** a Catalog table rename/move requires touching an Enrollment
  procedure. Mitigation: the join column is a single, stable identity (`Courses.Id`)
  plus the display title; if Catalog ever moves to another store, this procedure
  is the one to revisit (there is no other cross-module storage coupling to fix).
- **Rejected alternative — two-phase contract fetch:** filter/paginate
  enrollments in the Enrollment module, then resolve course titles via a Catalog
  contract for the page. Rejected because the title *filter* would then either
  need a full-table fetch (defeating paging) or a second paginated round trip
  with boundary drift — the join makes the predicate and the paging consistent.
- **Rejected alternative — denormalize `CourseTitle` onto `Enrollments`:**
  duplicates Catalog-owned data into the Enrollment table and requires
  write-path synchronization on course renames. Rejected: the read-only join
  has none of that cost.
