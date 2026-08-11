# Data Model: Admin Courses Management

**Date**: 2025-08-11
**Feature**: specs/025-admin-courses-management

## Existing Entities (No Changes)

### Course (Catalog.Domain)

Already exists in `src/Modules/Catalog/Domain/Course.cs`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | Primary Key | Auto-generated |
| Title | string | Required, Max 200 | Unique per Organization |
| ShortDescription | string | Required, Max 500 | Listing display |
| FullDescription | string | Required, Max 4000 | Detail page |
| Category | string | Required, Max 100 | Filter/sort field |
| Duration | string | Required, Max 50 | Free-text (e.g., "3 hours") |
| OrganizationId | Guid | Required | Owning organization |
| CreatedAt | DateTimeOffset | Required | Set on creation |

**Validations**:
- Title + OrganizationId must be unique (enforced by DB index)
- All string fields are required and non-empty

**State transitions**: None (CRUD only)

---

### CourseVisibilityOverride (Management.Domain)

Already exists. Not modified by this feature.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | Primary Key | Auto-generated |
| CourseId | Guid | Foreign Key → Course | Course being overridden |
| OrganizationId | Guid | Foreign Key → Organization | Org applying the override |
| IsHidden | bool | — | True = hidden from this org |
| CreatedBy | Guid? | Nullable | User who created the override |
| CreatedAt | DateTimeOffset | Required | Set on creation |

---

## New/Modified Application-Layer Types

### UpdateCourseRequest (NEW — Catalog.Endpoints)

Request DTO for updating a course.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Title | string | Required, Max 200 | Updated title |
| ShortDescription | string | Required, Max 500 | Updated short description |
| FullDescription | string | Required, Max 4000 | Updated full description |
| Category | string | Required, Max 100 | Updated category |
| Duration | string | Required, Max 50 | Updated duration |

**Rationale**: Mirrors `CreateCourseRequest` but for updates. Excludes `OrganizationId` (immutable after creation) and `CreatedAt` (immutable).

---

### CourseDisplay (EXISTING — Host.Pages.Admin.Courses)

Already exists as a record. May need additional fields for edit support.

| Field | Type | Notes |
|-------|------|-------|
| CourseId | Guid | — |
| Title | string | — |
| Category | string | — |
| OrganizationName | string | Resolved from OrganizationId |
| Source | string | "Local" or "Inherited" |
| Visibility | string | "Visible" or "Hidden" |

**No changes needed** — this DTO already carries all fields needed for the listing table.

---

## Relationships

```
Organization 1───────* Course
Course     1───────* CourseVisibilityOverride
Organization 1───────* CourseVisibilityOverride
```

No new entities or relationships are introduced by this feature. All changes are at the presentation and application service layers.
