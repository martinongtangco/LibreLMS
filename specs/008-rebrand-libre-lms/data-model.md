# Data Model: Rebrand to Libre LMS

**Created**: 2025-07-30
**Feature**: [spec.md](spec.md)

## Summary

This feature involves **no data model changes**. The database schema, entity definitions, and relationships remain identical.

The only data-related change is the **database name** in the connection string:
- **Before**: `LearningLms`
- **After**: `LibreLms`

## Database Name Change

| Property | Before | After |
|----------|--------|-------|
| Database name | `LearningLms` | `LibreLms` |
| Connection string location | `src/Host/appsettings.Development.json` | Same file |
| Impact | Existing database must be recreated with new name | New database `LibreLms` created on first run (seeded data applies) |

## Existing Entities (unchanged)

No entity definitions, fields, or relationships are modified by this rebrand. The following entities exist in the system and remain untouched:

- **Course** (Catalog): ID, Title, Description, Category, Duration
- **Student** (Enrollment): ID, Name, Email, PasswordHash, Roles
- **Enrollment** (Enrollment): ID, StudentId, CourseId, Status, EnrolledAt
- **ScormPackage** (Scorm): ID, CourseId, ManifestPath, UploadedAt
- **CourseAttempt** (Scorm): ID, StudentId, CourseId, Status, Score, CompletedAt
