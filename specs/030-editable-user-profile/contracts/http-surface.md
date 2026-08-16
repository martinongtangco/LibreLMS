# HTTP / Web Surface: Editable User Profile With Photo & Course History

**Feature**: [spec.md](../spec.md) | **Date**: 2026-08-16

User-facing web surface (Razor Pages in `src/Host/Pages`) plus one Development-only
page. Behavior contract — markup/styling follows the existing `_Layout` and form
patterns (`site.css`). No new minimal-API endpoints; no API endpoint changes.

---

## 1. GET /Account/Profile (CHANGED — was read-only)

| | |
|---|---|
| Auth | `[Authorize]` (unchanged); anonymous → sign-in redirect (FR-013) |
| Renders | Personal card: avatar (photo or initials placeholder), **editable Name field**, read-only Email, read-only Role; verification-state banner when `IsEmailVerified == false` ("a verified email is required to save changes" + **Resend verification link** button); photo form (file input, `accept=".jpg,.jpeg,.png,.webp,.gif"`) with current-photo preview; "My Courses" area with **Completed** and **Enrolled** sections (course title + status label per course; empty state "You haven't enrolled in any courses yet" when the user has no enrollments; friendly inline error in the courses area only when course data fails to load — personal details always render, FR-014) |
| Data | Fresh `Student` read for name/verified-state/avatar; enrollments + attempts join (R6) |
| Anti-forgery | Standard Razor Pages form token (as on Settings) |

## 2. POST /Account/Profile (name save) (NEW handler)

| | |
|---|---|
| Auth | `[Authorize]` |
| Form fields | `name` (text, maxlength 100) |
| Validation | FR-003: trimmed non-empty, ≤ 100 chars, no line breaks → field-level error, nothing persisted |
| Gate | `IsEmailVerified == false` → **no** update; page re-renders with the verification banner + resend button; success message suppressed (FR-002) |
| Success path | `IUserProvisioning.UpdateAsync(studentId, trimmedName, null, null)` → **auth cookie re-issued** from the fresh `Student` row (new `Name` + `AvatarPath` claims, R2/R3) → success message; new name visible in the upper-right nav on the resulting page (FR-004) |
| Errors | Service/DB failure → friendly error message, previous name retained |

## 3. POST /Account/Profile (photo save) (NEW handler)

| | |
|---|---|
| Auth | `[Authorize]` |
| Form fields | `avatar` (file) |
| Validation | FR-010: file present; extension + MIME ∈ {jpg, jpeg, png, webp, gif}; ≤ 5 MB → friendly error, previous photo unchanged |
| Success path | Write to `wwwroot/avatars/{studentId-lower}{ext}` (temp file → move); delete previous file if any; `Student.AvatarPath = /avatars/{file}`; **auth cookie re-issued** (new `AvatarPath` claim) → success message; photo visible on profile + nav (for FR-008 audience) on the resulting page |
| Errors | Write failure (disk) → friendly error, column + previous file untouched |

## 4. POST /Account/Profile (resend verification) (NEW handler)

| | |
|---|---|
| Auth | `[Authorize]` |
| Form fields | none (uses the signed-in user's email) |
| Behavior | `RegistrationService.ResendVerificationAsync(email, baseUrl)` — same neutral result message + 3/hour-per-email throttle as the Login page (R8) |

## 5. Upper-right nav — account control (CHANGED)

| | |
|---|---|
| Renders | Inside `.account-control`, **before** `.account-name`: `<img class="account-avatar" src="@AvatarPath-claim">` when the claim is non-empty, else `<span class="account-avatar account-avatar-fallback">A</span>` (first letter of the display name, uppercased) |
| Visibility | Always shown for users without an admin role; for admin-role users shown only in the **Learner** view — implemented as CSS `.role-admin .account-avatar { display: none; }` on the existing body class (R5). Name, role pill, chevron, and dropdown unchanged |
| Data | 100% claim-driven (`User.Identity.Name` + `AvatarPath` claim); no DB access in the layout |

## 6. Static avatar files (NEW, no endpoint)

| | |
|---|---|
| Route | `GET /avatars/{guid}.{ext}` — served by the existing static-files middleware from `wwwroot/avatars/` |
| Auth | none (GUID-keyed filename derived from the auth claim; see ADR 0007 for the public-URL trade-off) |
| Constraints | Only files written by handler 3 exist here; no user-controllable filenames; 404 for unknown files |

## 7. GET /Dev/Unverify (NEW, **Development-gated**)

| | |
|---|---|
| Auth | `[Authorize]` + `Environment.IsDevelopment()` (same gate as `/Dev/Outbox`); non-dev → 404 |
| Params | `email` (query) |
| Behavior | Flips `IsEmailVerified = false` for the student with that (normalized) email; renders "unverified {email}" or "no account for {email}". Purpose: make the FR-002 negative branch E2E-observable (R7). No production exposure |

## Unchanged

- All minimal-API endpoints (`/api/*`), all other Razor pages, sign-in/sign-out,
  `/Account/Verify`, `/Account/Settings`, My Courses, admin pages.
