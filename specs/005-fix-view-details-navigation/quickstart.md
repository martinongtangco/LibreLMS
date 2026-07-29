# Quickstart Validation: Fix Course View Details Navigation

**Purpose**: Validate that the "View Details" navigation fix works correctly across all scenarios.

**Prerequisites**:
- Application running (`dotnet run --project src/Host`)
- Database seeded with at least one course
- Browser with developer tools available

---

## Validation Scenario 1: Full-page navigation via "View Details" button

1. Navigate to `http://localhost:5000/Courses` (course catalog)
2. Click the **"View Details"** button on any course card
3. **Expected**: Browser navigates to `/Courses/Detail?id={guid}` and renders the full page with navbar, course details, and enroll/launch actions
4. **Expected**: The URL bar shows `/Courses/Detail?id={guid}` (no `handler=` parameter)

## Validation Scenario 2: Full-page navigation via course title link

1. Navigate to the course catalog
2. Click the **course title** (the heading link inside a course card)
3. **Expected**: Same result as Scenario 1 — full course detail page with layout

## Validation Scenario 3: Direct URL access (bookmark test)

1. Navigate to a course detail page via any method
2. Copy the URL from the address bar
3. Open a new browser tab and paste the URL
4. **Expected**: Full course detail page renders with navbar, course data, and action buttons
5. **Expected**: URL does NOT contain `?handler=Detail`

## Validation Scenario 4: Browser refresh on detail page

1. Navigate to a course detail page
2. Press **F5** or click the browser refresh button
3. **Expected**: Full page re-renders correctly with layout, course data, and action buttons
4. **Expected**: No "Course Not Found" or broken partial rendering

## Validation Scenario 5: Graceful degradation (JavaScript disabled)

1. Open browser developer tools → Application → disable JavaScript (or use a no-JS browser mode)
2. Navigate to the course catalog
3. Click "View Details" on a course card
4. **Expected**: Browser performs a standard full-page navigation to the course detail page via the `href` attribute
5. **Expected**: Detail page renders with full layout

## Validation Scenario 6: HTMX inline swap from catalog (when HTMX loaded)

1. Navigate to the course catalog with HTMX loaded
2. Open browser Network tab
3. Click "View Details" on a course card
4. **Expected**: An HTMX request fires to `/Courses/Detail?id={guid}&handler=Detail`
5. **Expected**: Response is the `_CourseDetail` partial HTML (no `<html>`, `<head>`, or `<body>` tags)
6. **Expected**: The partial is swapped into `#main-content` on the catalog page
7. **Expected**: Browser URL updates to `/Courses/Detail?id={guid}` (clean path, no `handler=`)
8. **Expected**: Pressing refresh now loads the full detail page correctly

## Validation Scenario 7: Course Not Found

1. Navigate to `/Courses/Detail?id=00000000-0000-0000-0000-000000000000` (non-existent GUID)
2. **Expected**: Page renders with "Course Not Found" message and a "Back to Catalog" link
3. **Expected**: Page still renders with full layout (navbar, footer)

## Validation Scenario 8: Browser back/forward navigation

1. Navigate: Catalog → Detail (click View Details) → Back to Catalog (browser back) → Detail again (click View Details)
2. **Expected**: Each navigation renders correctly, no broken pages
3. **Expected**: Browser history is consistent with page content

---

## Pass/Fail Criteria

| Scenario | Pass Criteria |
|----------|--------------|
| 1. View Details button | Full page renders, URL has no `handler=` param |
| 2. Title link | Same result as Scenario 1 |
| 3. Bookmark test | Detail page renders from direct URL |
| 4. Refresh | Full page re-renders correctly |
| 5. No JavaScript | Standard link navigation works |
| 6. HTMX inline swap | Partial swap + clean URL push |
| 7. Course Not Found | Error state with layout |
| 8. Back/forward | Consistent rendering |
