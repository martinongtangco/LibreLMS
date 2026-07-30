# Data Model: Clean Up Orphaned HTMX Handler

## Summary

This cleanup introduces **no data model changes**. It removes one server-side handler method and updates documentation files. No entities, view models, or database schema are affected.

## Affected Code

### `OnGetDetailAsync` method (to be removed)

- **Location**: `src/Host/Pages/Courses/Detail.cshtml.cs`
- **Signature**: `public async Task<PartialViewResult> OnGetDetailAsync(Guid id)`
- **Purpose**: HTMX handler that returned `Partial("_CourseDetail", model)` for inline course-detail swapping
- **Status**: Orphaned — no view calls it after spec 005 removed HTMX from `_CourseCard.cshtml`
- **Action**: Remove entirely

### `CourseDetailItem` record (unchanged)

- **Location**: `src/Host/Pages/Courses/Detail.cshtml.cs`
- **Used by**: `OnGetAsync` (full page) — still active
- **Previously used by**: `OnGetDetailAsync` (HTMX partial) — being removed
- **Action**: No change — still needed by `OnGetAsync`
