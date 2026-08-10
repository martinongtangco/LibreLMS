# Bug 024: Fix Dashboard Metrics Context Error

## Problem
The Admin Dashboard throws: `"Failed to load dashboard: Cannot use multiple context instances within a single query execution. Ensure the query uses a single context instance."`

## Root Cause
`DashboardService.GetRecentActivityAsync()` performs a LINQ `.Join()` across `enrollmentCtx.Enrollments`, `enrollmentCtx.Students`, and `catalogCtx.Courses` in one query expression. `enrollmentCtx` and `catalogCtx` are separate `DbContext` instances — EF Core cannot materialize a query spanning two contexts.

## Fix
Split the cross-context join into two queries:
1. Query `Enrollments` + `Students` from `EnrollmentDbContext`
2. Query `Courses` from `CatalogDbContext` 
3. Join the results in memory

## Constitution Principles
- **III. Module Boundaries Are Compiled** — Management module accesses Enrollment and Catalog through their Infrastructure DbContexts (already established pattern)
- **X. No Ad-Hoc Fixes** — documented before coding
