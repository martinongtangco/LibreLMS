# Data Model: Fix Course View Details Navigation

## Summary

This fix introduces **no new data entities**. It modifies only the client-side navigation behavior (HTMX attributes on `<a>` elements in Razor views). All data fetching, persistence, and business logic remain unchanged.

## Existing Entities (unchanged)

### Course
- **Source**: `Catalog` module, `CourseCatalogService.GetByIdAsync()`
- **Fields used on detail page**: `Id`, `Title`, `ShortDescription`, `FullDescription`, `Category`, `Duration`
- **No changes**: Same data shape, same fetch path

### Enrollment
- **Source**: `Enrollment` module, `IEnrollmentLookup.IsEnrolledAsync()`
- **Fields used on detail page**: Boolean enrollment status per student/course pair
- **No changes**: Same lookup pattern

### ScormPackage
- **Source**: `Scorm` module, `ScormPackageService.GetPackageByCourseIdAsync()`
- **Fields used on detail page**: Presence check (null = no SCORM, non-null = show Launch button)
- **No changes**: Same presence check

## View Models (unchanged)

### `CourseDetailItem` (in `Detail.cshtml.cs`)
Record used by both `OnGetAsync` (full page) and `OnGetDetailAsync` (HTMX partial). Fields: `Id`, `Title`, `ShortDescription`, `FullDescription`, `Category`, `Duration`, `IsEnrolled`, `IsScorm`, `ScormPackageId`.

**No changes required.**

## State Transitions

None — this fix does not alter any state machine or workflow. The enrollment/launch state displayed on the detail page is determined by existing service calls and is unaffected.
