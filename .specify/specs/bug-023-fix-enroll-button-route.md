# Bug 023: Enroll Button Route Fix — Missing Route Parameter in HTMX Form URLs

## Problem

The "Enroll now" button on the course detail page (`Courses/Detail.cshtml`) and the retry button in the enrollment result partial (`_EnrollmentResult.cshtml`) fail silently. Clicking either button produces no visible action.

## Root Cause

Both forms POST to `/Courses/Detail?handler=Enroll` via HTMX. However, the page route is `@page "{id:guid}"`, which requires a GUID as a **path segment** (e.g., `/Courses/Detail/{guid}`). Because the form URL omits the course ID from the path entirely, ASP.NET Core routing cannot match the request — it returns a 404 before the handler method is ever invoked.

The `hx-vals` attribute sends `courseId` as form data, but routing fails first, so the data is never consumed.

**Why buttons keep breaking (structural):** HTMX `hx-get`/`hx-post` attributes are plain strings with zero compile-time URL verification. Razor tag helpers (`asp-page`, `asp-page-handler`) generate verified URLs; raw strings do not. The `HtmxHandlerTests` catch missing handler methods but cannot validate URL path construction or missing route parameters.

## Affected Files

- `src/Host/Pages/Courses/Detail.cshtml` (line 48: enroll form)
- `src/Host/Pages/Shared/_EnrollmentResult.cshtml` (line 24: retry form)

## Fix

Replace hardcoded `hx-post="/Courses/Detail?handler=Enroll"` with URLs that include the required `{id}` route parameter:

```html
<!-- Detail.cshtml -->
<form hx-post="/Courses/Detail/@Model.Course.Id?handler=Enroll"
      hx-swap="outerHTML"
      hx-target="#enroll-region">

<!-- _EnrollmentResult.cshtml -->
<form hx-post="/Courses/Detail/@Model.CourseId?handler=Enroll"
      hx-swap="outerHTML"
      hx-target="#enroll-region">
```

Remove the now-redundant `hx-vals='{"courseId": "..."}` since the ID is in the URL path and bound by `[BindProperty(SupportsGet = true)] public Guid Id`.

## Constitution Principles Applied

- **VIII (Branching Discipline):** Working on `bug/023-fix-enroll-button-route`
- **X (No Ad-Hoc Fixes):** Root cause documented before coding
- **III (Module Boundaries):** No cross-module changes — Razor Pages views only
- **IV (Human-Legible Code):** Fix is explicit and self-documenting
