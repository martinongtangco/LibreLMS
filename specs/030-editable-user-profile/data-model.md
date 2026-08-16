# Data Model: Editable User Profile With Photo & Course History

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-16

One schema change total: a nullable column on `Student`. No new tables, no new
entities. Everything else is derived from existing data.

---

## Modified Entity: `Student` (Enrollment module, `EnrollmentDbContext`)

| Field | Type | Null | Constraints | Notes |
|-------|------|------|-------------|-------|
| `Id` | `Guid` | no | PK | unchanged |
| `Name` | `string` | no | max 100 (spec 030 FR-003) | **becomes editable** via self-service (page-level validation; stored as-is) |
| `Email` | `string` | no | unique (case-insensitive) | unchanged; read-only on the profile |
| `PasswordHash` | `string` | no | — | unchanged |
| `Roles` | `string` | no | — | unchanged; read-only on the profile |
| `OrganizationId` | `Guid` | no | — | unchanged |
| `CreatedAt` | `DateTimeOffset` | no | — | unchanged |
| `EmailNotificationsEnabled` | `bool` | no | default true | unchanged |
| `ThemePreference` | `string` | no | — | unchanged |
| `IsEmailVerified` | `bool` | no | default true | unchanged; **drives the FR-002 name-change gate** (checked from the DB at save time, not from claims) |
| `SecurityStamp` | `Guid` | no | — | unchanged; re-issued cookie carries the same stamp (R2) |
| `VerificationTokenHash` / `VerificationTokenExpiresAt` | `string?` / `DateTimeOffset?` | yes | — | unchanged; powers the resend affordance |
| `ResetTokenHash` / `ResetTokenExpiresAt` | `string?` / `DateTimeOffset?` | yes | — | unchanged |
| **`AvatarPath`** (NEW) | `string?` | **yes** | max 200 chars | URL path of the display photo, e.g. `/avatars/3f2c….png`; `null` = no photo (render placeholder). Set/cleared only by the profile photo save. Also copied into the `AvatarPath` cookie claim (R3). |

**Migration**: one new migration in `src/Host/Migrations/Enrollment/`
(`AddAvatarPathToStudent`) — `ALTER TABLE Students ADD AvatarPath NVARCHAR(200) NULL`.
Dev startup still drops/recreates, so the migration is the durable record, not a
runtime dependency.

### Validation rules (enforced in the Profile page model, R1)

- **Name** (FR-003): non-empty after trimming; ≤ 100 characters; no line-break
  characters (`\r`, `\n`); stored trimmed. Rejections render a field-level message and
  persist nothing.
- **Photo** (FR-010): file present; extension AND MIME type within
  {`jpg`/`image/jpeg`, `jpeg`, `png`/`image/png`, `webp`/`image/webp`,
  `gif`/`image/gif`} (case-insensitive); total size ≤ 5 MB. Rejections render a
  friendly message and leave the stored photo + column untouched.
- **Gate** (FR-002): name save proceeds only when `IsEmailVerified == true` for the
  signed-in student (fresh DB read at save time); otherwise refused + resend
  affordance (R8).

### State transitions

- **Email verification**: unchanged from spec 027 (`false → true` via verification
  link; single-use 24 h tokens). The profile only *reads* this state (and offers
  resend). No new transitions introduced by this feature.
- **Avatar**: `null → set` (first upload), `set → set` (replacement; old file
  deleted), `set → set` with different extension (old file deleted, new file written).
  There is no explicit "remove photo" action in this slice (spec does not require
  one); a replacement with a different image is the only mutation.

## Unchanged entities used by the feature (read-only)

- **`Enrollment`** (Enrollment module): `(Id, StudentId, CourseId, EnrolledAt)` —
  source of the profile course list via `GetMyEnrollmentsAsync` (returns enrollment +
  joined course title).
- **`CourseAttempt`** (Scorm module): `(Id, StudentId, CourseId, AttemptNumber, Status,
  ScoreRaw, …)` — `Status ∈ {in-progress, completed, abandoned, passed, failed}`;
  source of the Completed/Enrolled grouping and status labels via
  `GetMyAttemptsAsync`.
- **`Course`** (Catalog module): title surfaced through the enrollment join; no
  direct access from the profile.

### Derived view model (Profile page, not persisted)

| Property | Source |
|----------|--------|
| `Name` (editable) | `Student.Name` |
| `Email`, `RoleLabel` (read-only) | `Student` row (fresh read) / role claim |
| `IsEmailVerified` + resend state | `Student.IsEmailVerified` |
| `AvatarUrl` | `Student.AvatarPath` (claim used for nav) |
| `CompletedCourses[]` | enrollments where ∃ attempt with `Status ∈ {completed, passed}` |
| `EnrolledCourses[]` | remaining enrollments, each with latest-attempt status label (`ScormHelpers.GetDisplayLabel`) or neutral "Enrolled" when no attempt |

A course appears in exactly one list; Completed wins (R6).
