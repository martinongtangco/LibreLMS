# Quickstart Validation Guide: Admin List Pagination with Page Size Toggle

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Runnable validation scenarios proving the feature works end-to-end. Data shapes are in
[data-model.md](data-model.md); procedure signatures in
[contracts/stored-procedures.md](contracts/stored-procedures.md); query rules in
[contracts/admin-pages-query.md](contracts/admin-pages-query.md).

## Prerequisites

```bash
# from the repo root, inside the devcontainer (Constitution V)
docker compose up -d mssql valkey     # sibling services
dotnet build LibreLms.slnx           # must compile clean
```

The Host applies EF migrations (including the two new procedure migrations) and seeds the
catalog automatically at startup (`Program.cs`), so a normal app start is the migration
vehicle — no manual SQL needed.

## 1. Integration tests (stored-procedure contracts)

```bash
dotnet test tests/Catalog.Tests --filter "FullyQualifiedName~Browse"   # extended BrowseCourses: sort matrix, OrganizationId, legacy defaults
dotnet test tests/Enrollment.Tests --filter "FullyQualifiedName~AdminList"  # AdminListEnrollments + AdminListLearners: filters, paging math, determinism, clamping
dotnet test tests/ArchitectureTests                                        # Constitution III gate (module boundaries)
```

**Expected**: all green. These tests create marker-prefixed filler rows and clean them up,
so they pass regardless of prior data state.

## 2. Start the app

```bash
dotnet run --project src/Host        # → "Now listening on: http://localhost:5000"
```

**Expected**: startup log shows migrations applied (two new migration entries) and the seed
run; no errors.

## 3. E2E test (user-visible behavior, all three pages)

```bash
cd tests/Playwright.Tests
npx playwright test tests/16-admin-pagination.spec.ts
```

**Expected**: all tests pass. The spec is self-contained (creates marker-prefixed filler
learners/enrollments/courses in `beforeAll`, deletes them in `afterAll`) and covers, on each
of the three admin pages:

- page controls render when rows exceed one page; none when they don't
- Previous hidden on page 1; Next hidden on the last page
- page size toggle 10 → 50 → 100 re-renders at page 1 with the chosen row count
- filter + pagination compose (filter on page N → page 1 of filtered results)
- URL tampering: `pageSize=999` renders with size 10; `pageNumber=99999` renders the last valid page

Regression (Constitution XIII): the existing admin specs must still pass alongside:

```bash
npx playwright test tests/10-admin-courses.spec.ts tests/05-admin-learners.spec.ts tests/07-admin-enrollments.spec.ts tests/11-course-pagination.spec.ts
```

`11-course-pagination` is the guard for FR-017 (Browse Courses unchanged).

## 4. Manual smoke (SuperUser, `http://localhost:5000`)

1. Sign in as SuperUser → Admin view.
2. **Courses**: open Admin > Courses. With the default seeded catalog (10 courses ≤ page
   size 10) no pagination nav is visible. Create 11+ filler courses (or rely on test data),
   then verify: nav appears, sort headers reorder the *whole* filtered set (create one
   out-of-title-order course in each category to prove cross-page sorting), page size 30
   collapses the list to one page.
3. **Enrollments**: open Admin > Enrollments. Filter by a student name, confirm the total
   count in the indicator changes, paginate, change page size, confirm page 1 + retained size.
4. **Learners**: open Admin > Learners. Search an email substring, apply a role filter,
   paginate; confirm the organization column still shows org names (now resolved per page).
5. **Boundary actions**: on the last page, cancel the newest enrollment / delete a course —
   verify the page steps back when the current page empties, and the empty state renders when
   page 1 empties.
6. **Tamper checks** (address bar): append `pageSize=999` → 10 shown; `pageNumber=99999` →
   last page; `pageSize=15` → 10 shown (legacy sizes are no longer valid).

## Done-when summary

- Gates 1–3 of Constitution XIII evidenced: build output shown, Playwright spec + regressions
  pass, and after merge to `master` the same sequence re-runs green on merged code.
- No page ever renders more rows than the selected page size; indicator totals always match
  the filtered result count.
