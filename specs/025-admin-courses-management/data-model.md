# Data Model: Admin Courses Management with SCORM Integration

**Date**: 2025-08-11
**Feature**: specs/025-admin-courses-management

---

## Existing Entities (No Structural Changes)

### Course (Catalog.Domain)

Already exists in `src/Modules/Catalog/Domain/Course.cs`. No fields added or removed.

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

**Relationships**: 0..1 ScormPackage (a course may or may not have SCORM content)

---

### CourseVisibilityOverride (Management.Domain)

Already exists. Not modified by this feature.

---

## Modified Entities

### ScormPackage (Scorm.Domain) — CHANGED

In `src/Modules/Scorm/Domain/ScormPackage.cs`. The `CourseId` field becomes nullable.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | Primary Key | Auto-generated |
| **CourseId** | **Guid?** | **Nullable, Filtered Unique (non-null only)** | FK to Course; null = available pool |
| ManifestTitle | string | Required, Max 200 | Extracted from imsmanifest.xml |
| LaunchPath | string | Required | Relative path to launch HTML |
| ContentDirectory | string | Required | Server-relative path (e.g., "scorm-content/{Id}") |
| CreatedAt | DateTimeOffset | Required | Set on creation |

**Changes**:
- `CourseId` changes from `Guid` to `Guid?`
- Unique index on `CourseId` becomes a **filtered unique index**: only non-null values are unique
- Multiple packages can have `CourseId = null` (available pool)
- Each non-null `CourseId` must still be unique (one SCORM per course)

**Validations**:
- When `CourseId` is set, it must reference a valid Course (application-level check; no DB FK constraint since modules are separate)
- `ManifestTitle`, `LaunchPath`, `ContentDirectory` must be non-empty after upload

**State transitions**:
```
[Uploaded, CourseId=null] ──associate──→ [Associated, CourseId=courseId]
[Associated, CourseId=X]    ──replace───→ [Deleted] → [Uploaded, CourseId=X]
[Associated, CourseId=X]    ──course delete──→ [Deleted] (with confirmation warning)
[Uploaded, CourseId=null]   ──pool delete──→ [Deleted] (admin deletes from Upload page)
```

**Cascade delete**: When a Course is deleted, the associated ScormPackage is also deleted along with its content directory from the filesystem. The admin must confirm this via a warning dialog.

---

## EF Core Model Changes

### ScormDbContext — Index Change

In `src/Modules/Scorm/Infrastructure/ScormDbContext.cs`:

```csharp
// BEFORE:
entity.HasIndex(e => e.CourseId).IsUnique();

// AFTER:
entity.HasIndex(e => e.CourseId)
    .IsUnique()
    .HasFilter("[CourseId] IS NOT NULL");
```

### Migration: AddScormPackageNullableCourseId

```csharp
// src/Host/Migrations/Scorm/{timestamp}_AddScormPackageNullableCourseId.cs

protected override void Up(MigrationBuilder migrationBuilder)
{
    // Drop existing unique index
    migrationBuilder.DropIndex("ScormPackages", "IX_ScormPackages_CourseId");
    
    // Make column nullable
    migrationBuilder.AlterColumn<Guid>(
        name: "CourseId",
        table: "ScormPackages",
        nullable: true,
        oldClrType: typeof(Guid),
        oldType: "uniqueidentifier");
    
    // Create filtered unique index
    migrationBuilder.CreateIndex(
        name: "UX_ScormPackages_CourseId",
        table: "ScormPackages",
        column: "CourseId",
        unique: true,
        filter: "[CourseId] IS NOT NULL");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex("ScormPackages", "UX_ScormPackages_CourseId");
    
    // For rollback: existing null values need handling
    // Strategy: delete packages with null CourseId, then revert column
    migrationBuilder.Sql("DELETE FROM ScormPackages WHERE CourseId IS NULL");
    
    migrationBuilder.AlterColumn<Guid>(
        name: "CourseId",
        table: "ScormPackages",
        type: "uniqueidentifier",
        nullable: false,
        defaultValue: Guid.Empty,
        oldClrType: typeof(Guid),
        oldNullable: true);
    
    migrationBuilder.CreateIndex(
        name: "IX_ScormPackages_CourseId",
        table: "ScormPackages",
        column: "CourseId",
        unique: true);
}
```

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

---

### CreateCourseRequest — MODIFIED (Catalog.Endpoints)

Add optional `ScormPackageId` for the association flow.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Title | string | Required | — |
| ShortDescription | string | Required | — |
| FullDescription | string | Required | — |
| Category | string | Required | — |
| Duration | string | Required | — |
| OrganizationId | Guid? | Nullable | — |
| **ScormPackageId** | **Guid?** | **Nullable, NEW** | **Existing SCORM to associate; null = no association** |

---

### ScormPackageService — New Methods

| Method | Signature | Purpose |
|--------|-----------|---------|
| `ListAvailableAsync` | `Task<IEnumerable<ScormPackage>>` | Returns packages with `CourseId == null` for the association dropdown |
| `AssociateWithCourseAsync` | `Task<ScormPackage>(Guid packageId, Guid courseId)` | Sets `CourseId` of an available package; throws if already associated |
| `ReplacePackageAsync` | `Task<ScormPackage>(Guid courseId, Stream zipStream)` | Deletes existing package + content dir, uploads new package |

---

## Relationships

```
Organization 1────────* Course
Course     0..1───────* ScormPackage  (CourseId on ScormPackage, nullable)
Course     1────────* CourseVisibilityOverride
Organization 1────────* CourseVisibilityOverride
```

**Key relationship rules**:
- A Course can have 0 or 1 ScormPackage
- A ScormPackage can have 0 or 1 Course (null = available pool)
- A ScormPackage with a CourseId belongs exclusively to that course
- Multiple ScormPackages can have `CourseId = null` (available pool)
