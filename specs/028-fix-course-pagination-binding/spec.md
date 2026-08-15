# Bug Fix Specification: Course Page Pagination Does Nothing

**Feature Branch**: `bug/028-fix-course-pagination-binding`

**Created**: 2026-08-15

**Status**: Draft

**Input**: User report: "the Course page pagination doesn't do anything. Moving previous or next does nothing. there's 13 total fake courses, it displays 12, moving to next doesn't show the final 13th. Also if you're at the first page, previous button should not be visible. moving to the last page should make next button invisible too."

## Root Cause

Two independent defects in the Browse Courses page (spec 019, HTMX pagination):

### Defect 1: `page` query parameter is never bound (pagination does nothing)

`OnGetCourseListAsync` in `src/Host/Pages/Courses/Index.cshtml.cs` declares:

```csharp
public async Task<PartialViewResult> OnGetCourseListAsync(
    string? search,
    string? category,
    int page = 1)
```

ASP.NET Core infers the binding source for action parameters via `BindingSource.Infer`:
**an optional value-type parameter with a default value (`int page = 1`) is inferred as
`BindingSource.Form`, not `Query`.** The HTMX `hx-get` pagination requests send `page` as a
query-string parameter, so the value provider is never consulted and the handler always runs
with `page = 1`. Every Next/Previous click returns page 1 — the list never changes.

The `search` and `category` parameters (reference types) are inferred as `Query` and bind
correctly, which is why search/filter work while pagination silently does not.

**Runtime evidence (against the running app on http://localhost:5000):**

| Request | Result |
|---|---|
| `GET /Courses/Index?handler=CourseList&page=2` | "Page 1 of 2 (13 total)" — 12 courses (wrong) |
| `GET /Courses/Index?handler=CourseList&page=999` | "Page 1 of 2 (13 total)" — 12 courses (wrong) |
| `GET /Courses/Index?handler=CourseList` with form body `page=2` | "Page 2 of 2" — 1 course (proves `page` binds from Form only) |

This violates the spec 019 contract (`specs/019-course-search-pagination/contracts/browse-courses-htmx.md`),
which defines `page` as a query parameter of `GET /Courses/Index?handler=CourseList`.

### Defect 2: Previous/Next buttons are disabled but still visible at boundaries

`src/Host/Pages/Shared/_Pagination.cshtml` renders both buttons on every page and only adds
the `disabled` attribute at the boundaries. On page 1 the Previous button is still visible
(and generates a `page=0` URL); on the last page the Next button is still visible. The user
wants the out-of-range buttons **not rendered at all**.

## User Scenarios & Testing

### User Story 1 - Next/Previous Actually Change the Page (Priority: P1)

As a learner browsing courses, I want the Next and Previous buttons to move between pages,
so that I can see every course when the catalog exceeds one page.

**Independent Test**: With 13 seeded courses (page size 12), open /Courses/Index, click Next,
and confirm the 13th course appears on "Page 2 of 2".

**Acceptance Scenarios**:

1. **Given** 13 courses and page size 12, **When** I click Next on page 1, **Then** page 2 loads with the remaining 1 course and the indicator reads "Page 2 of 2 (13 total)"
2. **Given** I am on page 2, **When** I click Previous, **Then** page 1 reloads with the original 12 courses and "Page 1 of 2 (13 total)"
3. **Given** I am on page 2, **When** I re-request page 2 directly (`?handler=CourseList&page=2`), **Then** the server returns page 2 content (page parameter honors out-of-range capping: `page=999` returns the last valid page, not page 1)
4. **Given** a search or category filter is active, **When** I paginate, **Then** the filter is preserved and page resets to 1 on filter change (existing 019 behavior — regression check)

### User Story 2 - Boundary Buttons Are Hidden (Priority: P2)

As a learner, I want unavailable navigation hidden, so that the pagination bar only shows
actions that actually work.

**Acceptance Scenarios**:

1. **Given** I am on the first page, **When** I look at the pagination bar, **Then** no Previous button is rendered (not present in the DOM, not merely disabled)
2. **Given** I am on the last page, **When** I look at the pagination bar, **Then** no Next button is rendered
3. **Given** I am on a middle page (both boundaries reachable), **Then** both Previous and Next are rendered

## Implementation Notes

- Fix for Defect 1: annotate the handler parameter `[FromQuery] int page = 1` so the binding
  source is explicit. The existing `Math.Max(1, Math.Min(page, totalPages))` capping stays.
  The redundant `if (search != null || category != null)` reset block in the handler becomes
  dead logic once binding works (the filter-change flow already forces `page=1` via the
  `#page-reset` hidden field included by the search input/category select `hx-include`), so
  simplify it to keep the handler legible (Principle IV).
- Fix for Defect 2: in `_Pagination.cshtml`, render the Previous button only when
  `pageNumber > 1` and the Next button only when `pageNumber < totalPages`.
- No module-boundary changes (Host views + one Razor page handler only) — Principle III not triggered.
- No ADR required: root cause is a framework binding-inference pitfall, not a project design decision.
- Known limitation (out of scope): for org-scoped users the page math uses the unfiltered
  `TotalCount` while visibility filtering happens in C# after paging (pre-existing, spec 019 design).
- Stale seed-count failures in `02-course-browse.spec.ts` (Programming/Design category counts)
  predate this change (documented in spec 027 merge notes) and are not in scope.

## Verification (Principle XIII)

1. `dotnet build` passes; app restarted; `Now listening` + HTTP 200 shown.
2. New Playwright tests for both user stories pass; existing course-browse suite re-run
   (only pre-existing seed-count failures allowed, none new).
3. Post-merge to master: rebuild, restart, re-run Playwright suite.
