# Bug 025: Fix Enrollment Management Context Error

## Problem
The Enrollment Management page throws: `"Failed to load enrollments: Cannot use multiple context instances within a single query execution. Ensure the query uses a single context instance."`

## Root Cause
`AdminEnrollmentService.ListEnrollmentsAsync()` and `ListAllEnrollmentsAsync()` perform a LINQ `.Join()` across `enrollmentCtx.Enrollments`/`enrollmentCtx.Students` (from `EnrollmentDbContext`) and `catalogCtx.Courses` (from `CatalogDbContext`) in one query expression. EF Core cannot materialize a query spanning two `DbContext` instances.

This is the same root cause as bug 024 (Dashboard), applied to the Enrollment Management listing.

## Fix
In both `ListEnrollmentsAsync` and `ListAllEnrollmentsAsync`:
1. Query `Enrollments` + `Students` from `EnrollmentDbContext` first (applying org-scope and search filters)
2. Load matching `Courses` from `CatalogDbContext` by the collected course IDs
3. Join in memory using a `Dictionary<Guid, Course>`
4. Keep the existing `managementCtx` organization-name lookups (already done in memory)

## Constitution Principles
- **III. Module Boundaries Are Compiled** — Management module accesses Enrollment and Catalog through their Infrastructure DbContexts (established pattern)
- **X. No Ad-Hoc Fixes** — documented before coding
