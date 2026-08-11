# Quickstart Validation: Admin Courses Management

**Date**: 2025-08-11
**Feature**: specs/025-admin-courses-management

## Prerequisites

1. Database migrated and seeded (courses exist in the Catalog)
2. Application running with `dotnet run --project src/Host`
3. Logged in as a SuperUser or OrgAdmin user

## Validation Scenarios

### 1. View Course Listing with Contrast Fix

**Steps**:
1. Navigate to `/Admin/Courses`
2. Observe the table rendering

**Expected**:
- Table header is clearly visible against the page background (white card surface on beige page)
- Table rows have alternating background colors for easy scanning
- All courses are displayed in a paginated table (15 per page)
- Empty state shown if no courses exist

---

### 2. Create a New Course

**Steps**:
1. On `/Admin/Courses`, click "Create Course" button
2. Fill in: Title, Short Description, Full Description, Category, Duration
3. Click "Create Course"

**Expected**:
- Course is created and visible in the listing
- Success message displayed: "Course created successfully"
- Redirected back to `/Admin/Courses`

---

### 3. Search and Filter Courses

**Steps**:
1. On `/Admin/Courses`, enter a search term in the search box
2. Select a category from the dropdown

**Expected**:
- Results update to show only matching courses
- Results are paginated correctly
- Clear button resets all filters

---

### 4. Sort by Column

**Steps**:
1. On `/Admin/Courses`, click a column header (Title, Category, etc.)

**Expected**:
- Results reorder by the selected column
- Click again to toggle ascending/descending
- Sort indicator (▲/▼) shown on active column

---

### 5. Edit a Course

**Steps**:
1. On `/Admin/Courses`, click "Edit" on a course row
2. Modify a field (e.g., change the Title)
3. Click "Save Changes"

**Expected**:
- Changes are persisted
- Success message: "Course updated successfully"
- Redirected back to `/Admin/Courses` with updated data visible

---

### 6. Delete a Course

**Steps**:
1. On `/Admin/Courses`, click "Delete" on a course row
2. Confirm the deletion in the browser dialog

**Expected**:
- Course is removed from the database
- Success message: "Course deleted successfully"
- Course no longer appears in the listing
- If on the last page and it becomes empty, navigate to previous page

---

### 7. Empty State

**Steps**:
1. On `/Admin/Courses`, search for a term that matches no courses

**Expected**:
- Empty state message: "No courses match your search. Try adjusting your filters or create a new course."
- "Create Course" button remains visible

---

## Build and Run Commands

```bash
# Build
dotnet build src/Host

# Run
dotnet run --project src/Host

# Run architecture tests
dotnet test tests/ArchitectureTests
```

## Contract Reference

See [data-model.md](./data-model.md) for entity field constraints.
See [research.md](./research.md) for technical decisions.
