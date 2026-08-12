# Quickstart Validation: Admin Courses Management with SCORM Integration

**Date**: 2025-08-11
**Feature**: specs/025-admin-courses-management

## Prerequisites

1. Database migrated and seeded (courses exist in the Catalog; SCORM migration applied)
2. Application running with `dotnet run --project src/Host`
3. Logged in as a SuperUser or OrgAdmin user
4. A valid SCORM 1.2 ZIP package available for upload testing

## Validation Scenarios

### 1. View Course Listing with Contrast Fix

**Steps**:
1. Navigate to `/Admin/Courses`
2. Observe the table rendering

**Expected**:
- Table header is clearly visible against the page background (white card surface on beige page)
- Table rows have alternating background colors for easy scanning
- All courses are displayed in a paginated table (15 per page)
- Courses with SCORM content show a visual indicator (badge or column)
- Empty state shown if no courses exist

---

### 2. Create a New Course Without SCORM

**Steps**:
1. On `/Admin/Courses`, click "Create Course" button
2. Fill in: Title, Short Description, Full Description, Category, Duration
3. Leave SCORM option as "No SCORM content" (default)
4. Click "Create Course"

**Expected**:
- Course is created and visible in the listing without SCORM indicator
- Success message displayed: "Course created successfully"
- Redirected back to `/Admin/Courses`

---

### 3. Create a New Course with SCORM Upload

**Steps**:
1. On `/Admin/Courses`, click "Create Course" button
2. Fill in course details
3. Select "Upload new SCORM package" radio button
4. Choose a valid SCORM 1.2 ZIP file
5. Click "Create Course"

**Expected**:
- Course is created with SCORM content
- SCORM package is extracted and stored in `wwwroot/scorm-content/{Id}/`
- Course shows SCORM indicator in the listing
- Success message displayed

---

### 4. Create a New Course by Associating Existing SCORM

**Prerequisites**: A SCORM package exists in the available pool (uploaded via `/Admin/Upload` without a course)

**Steps**:
1. On `/Admin/Courses`, click "Create Course" button
2. Fill in course details
3. Select "Associate existing SCORM" radio button
4. Choose a SCORM package from the dropdown
5. Click "Create Course"

**Expected**:
- Course is created and linked to the selected SCORM package
- The SCORM package no longer appears in the "available" pool
- Course shows SCORM indicator in the listing
- Success message displayed

---

### 5. Search and Filter Courses

**Steps**:
1. On `/Admin/Courses`, enter a search term in the search box
2. Select a category from the dropdown

**Expected**:
- Results update to show only matching courses
- Results are paginated correctly
- Clear button resets all filters

---

### 6. Sort by Column

**Steps**:
1. On `/Admin/Courses`, click a column header (Title, Category, etc.)

**Expected**:
- Results reorder by the selected column
- Click again to toggle ascending/descending
- Sort indicator (▲/▼) shown on active column

---

### 7. Edit a Course (Metadata Only)

**Steps**:
1. On `/Admin/Courses`, click "Edit" on a course row
2. Modify a field (e.g., change the Title)
3. Click "Save Changes"

**Expected**:
- Changes are persisted
- Success message: "Course updated successfully"
- Redirected back to `/Admin/Courses` with updated data visible

---

### 8. Add SCORM to a Course Without SCORM

**Steps**:
1. On `/Admin/Courses`, click "Edit" on a course without SCORM
2. Upload a SCORM ZIP file in the SCORM section
3. Click "Save Changes"

**Expected**:
- SCORM package is created and associated with the course
- Course now shows SCORM indicator in the listing
- Success message displayed

---

### 9. Replace SCORM on a Course

**Steps**:
1. On `/Admin/Courses`, click "Edit" on a course with SCORM
2. Observe current SCORM package info (ManifestTitle, CreatedAt)
3. Upload a new SCORM ZIP file
4. Click "Save Changes"

**Expected**:
- Old SCORM package is deleted (entity + content directory)
- New SCORM package is created and associated
- Course shows updated SCORM info
- Success message displayed

---

### 10. Delete a Course with SCORM (Confirmation Warning)

**Steps**:
1. On `/Admin/Courses`, click "Delete" on a course with SCORM
2. Observe the confirmation dialog warns about SCORM deletion
3. Confirm the deletion

**Expected**:
- Confirmation dialog mentions that SCORM package and files will also be deleted
- Course is removed from the database
- Associated SCORM package is removed (entity + content directory)
- Success message: "Course deleted successfully"
- Course no longer appears in the listing

---

### 11. Upload SCORM to Available Pool (Admin/Upload page)

**Steps**:
1. Navigate to `/Admin/Upload`
2. Select a SCORM ZIP file (no course dropdown)
3. Upload

**Expected**:
- SCORM package is created with `CourseId = null`
- Package appears in the "Associate existing SCORM" dropdown on course creation
- Package appears in the "Available SCORM Packages" list on the Upload page
- Success message displayed

---

### 12. Delete Available SCORM from Pool

**Steps**:
1. Navigate to `/Admin/Upload`
2. Find an available (unassociated) SCORM package in the list
3. Click "Delete" on the package
4. Confirm deletion

**Expected**:
- SCORM package is removed from the database and filesystem
- Package no longer appears in the available pool
- Package no longer appears in the "Associate existing SCORM" dropdown
- Success message displayed

---

### 13. SCORM Without Course Cannot Be Launched

**Steps**:
1. Get the ID of a SCORM package with `CourseId = null`
2. Try to navigate to `/Scorm/Launch?courseId={someCourseWithoutScorm}`

**Expected**:
- Launch fails with an error: no SCORM package found for this course
- SCORM content is not accessible without course association

---

### 14. Empty State

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

# Run migrations (if needed)
dotnet ef database update --project src/Host --context ScormDbContext

# Run
dotnet run --project src/Host

# Run architecture tests
dotnet test tests/ArchitectureTests
```

## Contract Reference

See [data-model.md](./data-model.md) for entity field constraints and the SCORM-Course relationship.
See [research.md](./research.md) for technical decisions on transaction coordination and module boundaries.
