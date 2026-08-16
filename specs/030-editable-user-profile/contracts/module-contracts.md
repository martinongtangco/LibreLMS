# Module Contracts: Editable User Profile With Photo & Course History

**Feature**: [spec.md](../spec.md) | **Date**: 2026-08-16

**This feature adds ZERO new cross-module contracts.** The Host (composition root)
consumes four pre-existing surfaces. Documented here for completeness and to pin the
boundary decision (Constitution III).

---

## Consumed (existing) — Enrollment module

### `IUserProvisioning` (`Enrollment.Contracts`)

| Member | Use in this feature |
|--------|---------------------|
| `Task<StudentProvisionedDto?> GetByIdAsync(Guid studentId)` | Fresh account state for the profile render (name, `IsEmailVerified`, `AvatarPath` source) and the FR-002 gate |
| `Task<StudentProvisionedDto> UpdateAsync(Guid studentId, string? name, string? role, Guid? organizationId)` | Name save — called as `UpdateAsync(id, trimmedName, null, null)`; null role/org = no change (existing semantics) |

`StudentProvisionedDto` (existing record: `Id, Name, Email, Role, OrganizationId,
CreatedAt, IsEmailVerified`) **gains one field**: `string? AvatarPath` — the avatar's
URL path (or `null`). This is an additive, backward-compatible change to an existing
DTO; implementers must add it in the Enrollment module's DTO + mapping and keep all
existing call sites compiling.

### `RegistrationService` (Enrollment Application — Host injects it directly, as Login/Settings already do)

| Member | Use in this feature |
|--------|---------------------|
| `Task<bool> IsEmailVerifiedAsync(Guid studentId)` | FR-002 gate at save time |
| `Task<ResendResult> ResendVerificationAsync(string email, string baseUrl)` | In-profile resend (R8); existing throttle applies |

## Consumed (existing) — Enrollment + Scorm Application (Host)

| Member | Use in this feature |
|--------|---------------------|
| `EnrollmentService.GetMyEnrollmentsAsync(Guid studentId)` | Profile course list (enrollment + course title) |
| `ScormAttemptService.GetMyAttemptsAsync(Guid studentId)` | Attempt statuses for the Completed/Enrolled grouping + labels (R6) |

## New Host-internal surface (not a module contract)

### `AvatarClaimTypes` (`src/Host/ManagementAuth`)

| Member | Value |
|--------|-------|
| `const string AvatarPath` | `"AvatarPath"` — custom claim type; value = avatar URL path or absent |

Set alongside the existing claims at sign-in (`LoginModel.OnPostAsync`) and on every
cookie re-issue (R2). Mirrors `OrgClaimTypes` in location and style.

### `AuthCookieRefresher` (`src/Host/ManagementAuth`)

| Member | Contract |
|--------|----------|
| `Task RefreshAsync(HttpContext, StudentProvisionedDto)` | Builds the claim list (`NameIdentifier`, `Name`, `Email`, `SecurityStamp`, `Role` when set, `AvatarPath` when set) from the passed account state, creates a `ClaimsIdentity` for the `"Cookie"` scheme, and signs in with `IsPersistent = true`. Used by the Profile page after name/photo saves. Must produce claims **identical in shape** to `LoginModel.OnPostAsync` so the `OnValidatePrincipal` stamp check and role authorization are unaffected |

## Boundary impact

- No module references another module's internals anywhere in this feature.
- The only module-internal change is `Student.AvatarPath` + its `StudentProvisionedDto`
  projection (same module — allowed).
- `ModuleBoundaryTests` must stay green (14/14) — verified in quickstart.
