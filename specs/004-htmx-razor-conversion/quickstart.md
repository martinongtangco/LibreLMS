# Quickstart: HTMX + Razor Modern UI Validation

**Feature**: 004-htmx-razor-conversion
**Date**: 2025-07-28

## Prerequisites

- .NET 10 SDK (pinned via `global.json`)
- Docker + Docker Compose (for MSSQL and Valkey)
- Modern browser with JavaScript enabled

## Setup

```bash
# Start infrastructure
docker compose up -d

# Run the application
cd src/Host
dotnet run
```

The application starts at `http://localhost:5000` (or the port from `launchSettings.json`).

---

## Validation Scenarios

### Scenario 1: Catalog Filter Without Page Reload (P1)

**Steps**:
1. Navigate to `http://localhost:5000/Courses`
2. Open browser DevTools → Network tab
3. Type a search term in the search box
4. Wait 300ms after stopping typing

**Expected**:
- Course list updates within 1 second
- Network tab shows a single XHR request to `/Courses/Index?handler=CourseList&search=...`
- Navbar, footer, and URL remain unchanged
- Only the `#course-list` div content is swapped

**Failure indicators**:
- Full page reload occurs
- Network tab shows a full HTML document response (not a partial fragment)
- Layout shift or flash during the swap

---

### Scenario 2: Category Filter Without Page Reload (P1)

**Steps**:
1. Navigate to `http://localhost:5000/Courses`
2. Select a category from the dropdown

**Expected**:
- Course list updates to show only courses in that category
- No full page reload
- Category filter value persists in the dropdown

**Failure indicators**:
- Full page reload
- Category dropdown resets to "All Categories"

---

### Scenario 3: Enroll Inline (P1)

**Steps**:
1. Navigate to a course detail page (e.g., `http://localhost:5000/Courses/Detail?id=<guid>`)
2. Click "Enroll in This Course"

**Expected**:
- Enroll button is replaced with an "Enrolled" badge within 2 seconds
- If the course is SCORM, a "Launch SCORM Course" button appears
- No full page reload
- Network tab shows XHR POST to `/Courses/Detail?handler=Enroll`

**Failure indicators**:
- Page reloads after enrollment
- Button text changes but layout shifts
- Error message is not visible on failure

---

### Scenario 4: Already Enrolled Feedback (P1)

**Steps**:
1. Navigate to a course you are already enrolled in
2. Attempt to enroll again (if button is visible) or refresh and verify enrollment badge shows

**Expected**:
- "Already enrolled" message appears inline
- No full page reload
- No error thrown

---

### Scenario 5: Course Detail Inline Navigation (P2)

**Steps**:
1. Navigate to `http://localhost:5000/Courses`
2. Click a course title

**Expected**:
- Course detail loads in the main content area
- Navbar and footer remain stable
- No full page reload

**Failure indicators**:
- Full page navigation occurs
- Browser URL does not update (if `hx-push-url` is used)

---

### Scenario 6: My Courses Refresh (P2)

**Steps**:
1. Navigate to `http://localhost:5000/MyCourses`
2. Click the refresh trigger (if implemented) or wait for auto-refresh

**Expected**:
- Enrollment list updates without full page reload
- SCORM status badges reflect current data
- Layout remains stable

---

### Scenario 7: Graceful Degradation (FR-008)

**Steps**:
1. Disable JavaScript in browser settings
2. Navigate to `http://localhost:5000/Courses`
3. Use search and category filter (standard form submit)
4. Click a course title (standard link navigation)

**Expected**:
- All features work via full-page navigation
- Search form submits and page reloads with filtered results
- Course links navigate to detail pages normally
- No broken links or missing content

---

### Scenario 8: Architecture Tests Pass

```bash
dotnet test tests/ArchitectureTests
```

**Expected**: All module boundary tests pass. No new violations from HTMX changes.

---

## Rollback

If HTMX causes issues, removing the `<script>` tag from `_Layout.cshtml` disables all HTMX behavior. All HTML elements have standard `action`/`href` fallbacks, so the site degrades to full-page navigation immediately.
