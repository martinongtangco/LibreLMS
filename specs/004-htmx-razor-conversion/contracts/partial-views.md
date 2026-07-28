# HTMX Partial View Contracts

**Feature**: 004-htmx-razor-conversion
**Date**: 2025-07-28

These contracts define the interface between Razor Page handlers (which produce data) and partial views (which render HTML fragments for HTMX swaps).

---

## Contract: `_CourseList.cshtml`

**Purpose**: Renders the full course listing grid. Primary HTMX swap target for catalog filtering.

**HTMX Triggers**:
- `hx-get="/Courses/Index?handler=CourseList"` on search input (debounced)
- `hx-get="/Courses/Index?handler=CourseList"` on category select change
- `hx-get="/Courses/Index?handler=CourseList"` for back-from-detail navigation

**Request Parameters**:
| Parameter | Type | Source | Required |
|-----------|------|--------|----------|
| search | `string?` | Query string | No |
| category | `string?` | Query string | No |

**Model Type**: `IEnumerable<CourseItem>`

**Output Region**: Full course card grid (replaces `#course-list` div)

**HTMX Attributes on Target Container**:
```html
<div id="course-list" 
     hx-get="/Courses/Index?handler=CourseList"
     hx-trigger="from:#search-input"
     hx-indicator=".htmx-spinner">
```

**Error Behavior**: On failure, renders empty state with error message within the same region.

---

## Contract: `_CourseCard.cshtml`

**Purpose**: Renders a single course card. Included by `_CourseList.cshtml`.

**Model Type**: `CourseItem` (single item, not enumerable)

**Output**: Single `<div class="card">` with course title, description, badges, and detail link.

**HTMX Attributes**:
- Course title link: `hx-get="/Courses/Detail?id={Id}&handler=Detail" hx-target="#main-content" hx-push-url="true"`
- "View Details" button: Same as title link

**Nested in**: `_CourseList.cshtml` (rendered via `@Html.Partial()` or `@foreach`)

---

## Contract: `_EnrollmentResult.cshtml`

**Purpose**: Renders inline enrollment feedback. Swaps the enroll button region.

**HTMX Trigger**:
- `hx-post="/Courses/Detail?id={courseId}&handler=Enroll"` on enroll button click

**Model Type**: `EnrollmentResult`

**Output**: One of:
- Success: Enrolled badge + SCORM launch button (if applicable)
- Warning: "Already enrolled" message
- Error: Error message + retry button

**HTMX Attributes**:
```html
<form hx-post="/Courses/Detail?handler=Enroll"
      hx-vals='{"courseId": "..."}'
      hx-swap="outerHTML"
      hx-target="#enroll-region">
```

**Swap Mode**: `outerHTML` — replaces the entire enroll button/form region

---

## Contract: `_EnrollmentList.cshtml`

**Purpose**: Renders the full enrollments table for "My Courses". HTMX refresh target.

**HTMX Trigger**:
- `hx-get="/MyCourses?handler=Enrollments"` on manual refresh button
- `hx-get="/MyCourses?handler=Enrollments" hx-trigger="every 30s"` (optional auto-refresh)

**Model Type**: `IEnumerable<EnrollmentRow>`

**Output**: Table/grid of enrollment rows with status badges and scores.

**HTMX Attributes on Target**:
```html
<div id="enrollment-list"
     hx-get="/MyCourses?handler=Enrollments"
     hx-trigger="from:[data-refresh-enrollments]">
```

---

## Contract: `_MyCourseRow.cshtml`

**Purpose**: Renders a single enrollment row. Included by `_EnrollmentList.cshtml`.

**Model Type**: `EnrollmentRow` (single item)

**Output**: Row with course title link, enrollment date, status badge, score (if available).

**Nested in**: `_EnrollmentList.cshtml`

---

## Contract: `_ErrorPartial.cshtml`

**Purpose**: Generic error message for any HTMX swap failure.

**Model Type**: `string` (error message text)

**Output**: Simple `<div class="error-message">` with the error text.

**Triggered by**: Page handler detecting service/API failures and returning `Partial("_ErrorPartial", errorMessage)`

**HTMX Header Detection**: `Request.Headers["HX-Request"] == "true"`

---

## HTMX Request/Response Header Conventions

| Direction | Header | Purpose |
|-----------|--------|---------|
| Client → Server | `HX-Request: true` | Identifies HTMX requests (used for error handling) |
| Client → Server | `HX-Target: #course-list` | Tells server which element is the swap target |
| Server → Client | `HX-Trigger: reload` | (Optional) Tells HTMX to reload page after swap |
| Server → Client | `HX-Redirect: /MyCourses` | (Optional) Redirects after enrollment success |

**Note**: For this feature, server-sent headers are not required. Standard `hx-swap` behavior is sufficient.
