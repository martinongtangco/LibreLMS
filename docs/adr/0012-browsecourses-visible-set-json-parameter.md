# ADR-0012: BrowseCourses takes the visible-course set as a JSON parameter (OPENJSON)

**Status**: Accepted
**Date**: 2026-09-14
**Supersedes**: none

## Context

`BrowseAsync` filters the visibility-restricted course set **after** the
`BrowseCourses` SP has paged and counted, so pages can be short, the
reported total is inflated, and advertised pages can render empty (spec
054). The filter must move into the SP. The handoff leaves the shape open:
"table-valued parameter or a JSON array parameter".

Options:

1. **TVP** — `CREATE TYPE CourseIdList AS TABLE (CourseId UNIQUEIDENTIFIER)`
   + `@VisibleCourseIds CourseIdList READONLY` + a C# POCO mapped to the
   type.
2. **JSON parameter** — `@VisibleCourseIds NVARCHAR(MAX) = NULL` +
   `OPENJSON(... WITH ([value] UNIQUEIDENTIFIER))` in both SELECTs.
3. Comma-separated string + `STRING_SPLIT` — no, GUIDs need typed handling
   and `STRING_SPLIT` on 500+ ids is the same machinery as OPENJSON with
   worse semantics. Rejected.

## Decision

**JSON parameter (option 2).**

- **No DDL**: a TVP requires a user-defined table type — a second object to
  create, version, and drop in `Down`. OPENJSON needs only the parameter.
- **Plumbing is trivial**: one `NVARCHAR(MAX)` SqlParameter whose value is
  `string.Empty → DBNull`, `[]`, or a JSON array of GUID strings. No POCO/
  type-mapping machinery, no `SqlDbType.Structured` quirks.
- **Server support**: OPENJSON is native since SQL Server 2016; this repo
  runs 2022 (CI + devcontainer).
- **Semantics**: `NULL` = no visibility restriction (unauthenticated and
  no-org callers keep today's behavior — the parameter defaults to NULL, so
  any caller that doesn't pass it is untouched); a JSON array = rows and
  count restricted to the set; `[]` = nothing visible (fixes the current
  empty-set edge where an org with zero visible courses sees everything).
- The predicate is added to **both** the row SELECT and the COUNT SELECT,
  so paging and counting agree by construction:
  `AND (@VisibleCourseIds IS NULL OR c.Id IN (SELECT [value] FROM
  OPENJSON(@VisibleCourseIds) WITH ([value] UNIQUEIDENTIFIER)))`.

**Rejected**

- *TVP*: functionally identical, but adds a DDL object + C# mapping POCO
  for no behavioral gain on this server version; the handoff allows either,
  so the simpler artifact wins (Constitution II).
- *Carrying the filter in the query provider (EF LINQ) instead of the SP*:
  BrowseCourses is a hand-rolled SP (full-text-friendly LIKE, sort
  normalization, OFFSET/FETCH); re-deriving the catalog query in LINQ would
  duplicate and drift from the SP. Rejected.

## Consequences

**Positive**

- Filtering, paging, and counting live in one place; they cannot disagree.
- `NULL` default keeps every existing call site behavior-identical until it
  opts in.
- Migration is the house idempotent DROP+CREATE pattern (with the
  `.Designer.cs` partial — spec 052 gotcha).

**Negative**

- The visible set is serialized to JSON on every scoped browse call (a
  handful of hundred GUIDs at most in this product — negligible vs the
  page query).
- The SP body grows a predicate; the `WITH` clause pins the JSON shape
  (flat array of GUID strings) — documented on the parameter.

## Related

- ADR-0010 (org scope: where the visible set comes from —
  `GetVisibleCoursesAsync` + `IsHidden`)
- spec 047 (IsHidden row filtering — the half this completes)
- spec 048 (single-SP-call paging, the clamp this makes unreachable)
