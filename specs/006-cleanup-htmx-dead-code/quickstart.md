# Quickstart Validation: Clean Up Orphaned HTMX Handler

**Purpose**: Validate that the dead code removal and documentation updates are correct and do not break the application.

**Prerequisites**:
- Application builds successfully (`dotnet build LearningLms.slnx`)
- Application is running (`dotnet run --project src/Host`)
- Database seeded with at least one course

---

## Validation Scenario 1: Dead code is removed

1. Run `grep -rn "OnGetDetailAsync" src/Host/ --include="*.cs" --include="*.cshtml"`
2. **Expected**: Zero hits (method is fully removed)

## Validation Scenario 2: Application builds without errors

1. Run `dotnet build LearningLms.slnx`
2. **Expected**: Build succeeds with no errors related to `Detail.cshtml.cs`

## Validation Scenario 3: Full-page navigation still works

1. Navigate to `http://localhost:5000/Courses` (course catalog)
2. Click **"View Details"** on any course card
3. **Expected**: Browser navigates to `/Courses/Detail?id={guid}` and renders full page with layout
4. Click the **course title** link
5. **Expected**: Same result — full course detail page with layout

## Validation Scenario 4: Browser refresh works

1. Navigate to a course detail page
2. Press **F5**
3. **Expected**: Full page re-renders correctly with layout, course data, and action buttons

## Validation Scenario 5: tasks.md is updated

1. Open `specs/005-fix-view-details-navigation/tasks.md`
2. Read T004 and T005 descriptions
3. **Expected**: Descriptions reflect that HTMX was removed (not modified) from `_CourseCard.cshtml`
4. Read T008-T010 descriptions
5. **Expected**: Tasks are annotated as superseded by the full-page navigation approach

## Validation Scenario 6: spec.md is updated

1. Open `specs/005-fix-view-details-navigation/spec.md`
2. Read User Story 4
3. **Expected**: US4 is annotated as intentionally abandoned with rationale
4. Read FR-006
5. **Expected**: FR-006 notes that full-page navigation is the primary approach, not a fallback

---

## Pass/Fail Criteria

| Scenario | Pass Criteria |
|----------|--------------|
| 1. Dead code removed | Zero `OnGetDetailAsync` references in source |
| 2. Build succeeds | No compilation errors |
| 3. Navigation works | Both title and button navigate to full detail page |
| 4. Refresh works | Full page re-renders correctly |
| 5. tasks.md updated | T004/T005 accurate, T008-T010 annotated |
| 6. spec.md updated | US4 annotated, FR-006 updated |
