# Data Model: Course Catalog & Enrollment

**Date**: 2025-07-29
**Feature**: [spec.md](./spec.md)

## Entities

### Course

Represents a learnable unit of content. Stored in the `Catalog` module.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | `Guid` | Primary key, auto-generated | Unique course identifier |
| Title | `string` | Required, max 200 chars | Display name for the course |
| ShortDescription | `string` | Required, max 500 chars | Brief summary shown in catalog listings |
| FullDescription | `string` | Required, max 5000 chars | Detailed description shown on course detail page |
| Category | `string` | Required, max 100 chars | Flat category label (e.g., "Programming", "Design") |
| Duration | `string` | Required, max 50 chars | Human-readable duration (e.g., "2 hours", "5 weeks") |
| CreatedAt | `DateTimeOffset` | Required, set on insert | Record creation timestamp |

**Validation rules**:
- Title must be non-empty and trimmed
- Category values are free-text but should be normalized to a controlled vocabulary in seed data
- Duration is stored as a human-readable string (not a TimeSpan) to allow flexible formats

**Relationships**:
- One-to-many with Enrollment (one course has many enrollments)
- One-to-many with Student via Enrollment (one course is enrolled by many students)

### Enrollment

Represents the relationship between a Student and a Course. Stored in the `Enrollment` module.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | `Guid` | Primary key, auto-generated | Unique enrollment identifier |
| StudentId | `Guid` | Required, foreign key → Student | The student who enrolled |
| CourseId | `Guid` | Required, foreign key → Course | The course enrolled in |
| EnrolledAt | `DateTimeOffset` | Required, set on insert | When the enrollment was created |

**Validation rules**:
- (StudentId, CourseId) must be unique — prevents duplicate enrollment (FR-005)
- Both foreign keys must reference existing records

**Relationships**:
- Many-to-one with Student (many enrollments per student)
- Many-to-one with Course (many enrollments per course)

**State**: Enrollment is immutable in this slice — created once, never modified or deleted. (Un-enrollment is out of scope per research.md.)

### Student

Represents a learner on the platform. Stored in the `Enrollment` module (Enrollment module owns the student-enrollment relationship).

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | `Guid` | Primary key, auto-generated | Unique student identifier |
| Name | `string` | Required, max 200 chars | Display name for the student |
| Email | `string` | Required, max 320 chars, unique, valid email format | Student's email address |
| CreatedAt | `DateTimeOffset` | Required, set on insert | Record creation timestamp |

**Validation rules**:
- Email must be unique across students
- Email must pass basic format validation

**Relationships**:
- One-to-many with Enrollment (one student has many enrollments)

## Module Ownership

| Entity | Module | Rationale |
|--------|--------|-----------|
| Course | Catalog | Core catalog domain concept |
| Enrollment | Enrollment | Core enrollment domain concept |
| Student | Enrollment | Closely coupled with enrollment; the Enrollment module needs to validate student-course pairs |

## Cross-Module Dependency

The `Enrollment` module needs to know about Courses to validate enrollment targets. Per constitution Principle III, this is exposed through `Catalog.Contracts`:

- `Catalog.Contracts` will expose a `CourseSummary` DTO (Id, Title) used by the Enrollment module to validate that a course exists before creating an enrollment.
- The Enrollment module references `Catalog.Contracts`, never `Catalog` internals directly.

## Database Schema (EF Core)

All entities map to MSSQL tables via EF Core Code-First migrations:

- `Courses` table (Catalog module DbContext)
- `Students` table (Enrollment module DbContext)
- `Enrollments` table (Enrollment module DbContext)

Each module has its own `DbContext` to maintain isolation. Foreign key from `Enrollments.CourseId` to `Courses.Id` is enforced at the application level (via Catalog.Contracts lookup) rather than a database-level FK across module boundaries, keeping the DbContexts independent.
