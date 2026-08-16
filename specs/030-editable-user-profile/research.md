# Research: Editable User Profile With Photo & Course History

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-16

All technical unknowns from the plan's Technical Context are resolved below. No
NEEDS CLARIFICATION markers remain — the only spec-level ambiguity (nav photo audience,
FR-008) was already resolved by the user during `/speckit.specify` (Q1 = option C).

---

## R1. Where the name-edit + verification-gate logic lives

**Decision**: The `Account/Profile` page model (Host) calls **existing** surfaces only:
`IUserProvisioning.GetByIdAsync` / `UpdateAsync(studentId, name, null, null)` for the
name, and `RegistrationService.IsEmailVerifiedAsync(studentId)` /
`ResendVerificationAsync(email, baseUrl)` for the gate + resend. No Enrollment-module
code changes beyond the avatar column (R4).

**Rationale**: Host is the composition root and already injects module Application
services directly (Settings page → `EnrollmentService`, MyCourses →
`EnrollmentService` + `ScormAttemptService`). `IUserProvisioning.UpdateAsync` already
supports name-only updates (null role/org = no change). Reusing both keeps the
Enrollment module untouched and adds zero new cross-module surfaces (Constitution III).
Spec 030 name validation (FR-003: trimmed non-empty, ≤ 100 chars, no line breaks) is
enforced in the page model before calling the contract.

**Alternatives considered**:
- New `IProfileService` / `SelfServiceProfileService` in Enrollment Application —
  rejected: a new cross-module surface to wrap two existing calls; more surface to
  keep boundary-clean for no behavioral gain (II).
- Page talks to `EnrollmentDbContext` directly — rejected: bypasses service-level
  guards and is inconsistent with the Settings-page pattern.

## R2. Making the new name/photo appear in the nav without a layout DB hit

**Decision**: **Re-issue the auth cookie after each successful profile change**
(ASP.NET Core "RefreshSignIn" pattern). A small Host helper (`AuthCookieRefresher`)
rebuilds the claims list from the fresh `Student` row — `NameIdentifier`, `Name`,
`Email`, `SecurityStamp`, `Role` (when set), plus the new `AvatarPath` claim (R3) —
and calls `SignInAsync` again. The layout keeps reading `User.Identity.Name` and the
new claim, exactly as it does today.

**Rationale**: The auth cookie embeds `ClaimTypes.Name` at login
(`LoginModel.OnPostAsync`); without re-issue, FR-004 ("new name visible on the next
rendered page") would fail — the nav would show the stale name until re-login. The
cookie's `OnValidatePrincipal` stamp re-check (spec 027) stays green because the
`SecurityStamp` claim is unchanged. Re-issue happens only on the two mutating actions
(name save, photo save), so the cost is two sign-ins per change, not per request.

**Alternatives considered**:
- Per-request DB lookup in `_Layout` for name/avatar — rejected: async data loading in
  a shared layout is awkward, adds a query to every page render, and duplicates the
  claim mechanism the app already has.
- Client-side cache (localStorage) of the new name — rejected: stale across
  devices/tabs and after admin edits; contradicts the claim-based layout.

## R3. How the layout learns the avatar URL

**Decision**: One new cookie claim, `AvatarClaimTypes.AvatarPath`
(`src/Host/ManagementAuth/AvatarClaimTypes.cs`, mirroring the existing
`OrgClaimTypes` pattern). Value = the avatar's URL path (`/avatars/<guid>.<ext>`), set
at sign-in and on every cookie re-issue (R2). The layout renders
`<img class="account-avatar" src="@claim">` when the claim is non-empty, otherwise an
initials placeholder (first letter of `User.Identity.Name`, uppercased).

**Rationale**: Keeps the layout 100% claim-driven (no injection of services or async
data), consistent with how `OrganizationId` already flows. Storing a URL path (not a
server path) in both the DB column and the claim keeps the value presentable and
deployment-agnostic.

**Alternatives considered**:
- Query `IUserLookup`/context from the layout — rejected (see R2).
- Claim carrying a disk path — rejected: leaks server layout into cookies and HTML.

## R4. Where and how the display photo is stored

**Decision**: Static files under `src/Host/wwwroot/avatars/`, filename
`{studentId (lower-case GUID)}{extension}` — the GUID comes from the auth claim, never
from user input, so there is no path-traversal or enumeration surface. `Student` gains
one nullable column `AvatarPath` (URL path string, ≤ 200 chars); a new Enrollment
migration adds it. Upload handler (Profile page model): validate non-empty file,
extension/MIME in {jpg, jpeg, png, webp, gif} (case-insensitive), size ≤ 5 MB; write to
a temp file then move into place; delete the previous file when replaced; update the
column; re-issue the cookie (R2). Served by the existing static-files middleware — no
new endpoint. Invalid uploads leave the previous photo untouched (FR-010).
Recorded in **ADR 0007** (Constitution IV).

**Rationale**: The app already stores durable, file-shaped content on disk under
wwwroot (SCORM content via `ScormPackageService` with the `wwwRootPath` pattern).
MSSQL is for relational state (Constitution VI); image bytes there would bloat the
system of record with no relational need. Valkey is out (ephemeral — losing an avatar
on a flush is not fine).

**Alternatives considered**:
- BLOB column in MSSQL — rejected: VI (no relational guarantee needed; storage bloat).
- Valkey — rejected: ephemerality fails the "would losing this be fine?" test.
- Authenticated download endpoint (`/avatars/download?id=`) — rejected: per-request
  auth + endpoint + DB hit for a decorative image; GUID-keyed names are unguessable,
  and the avatar is not confidential data in this exercise (documented in ADR 0007:
  avatar URLs are effectively public to anyone who knows them).

## R5. Nav-menu photo visibility for admins (resolved Q1 = option C)

**Decision**: Zero new JavaScript. The layout already toggles `role-admin` /
`role-learner` classes on `<body>` from the role pill (localStorage `nav-role-view`,
default `'admin'` for admin-capable users; pure Learners return early and never get
`role-admin`). Add the avatar element inside `.account-control` and one CSS rule:
`.role-admin .account-avatar { display: none; }`.

**Resulting behavior** (matches FR-008 exactly): non-admin users always see the photo
(or placeholder); admin-role users see it only while the nav is in the Learner view,
never in the Admin view. The profile page itself always shows the photo regardless of
view.

**Alternatives considered**:
- Server-rendered conditional — rejected: the view is client-side state (localStorage),
  the server does not know it at render time.
- Extra JS to toggle the avatar — rejected: the body-class hook already exists and is
  the single source of truth for view-specific visibility.

## R6. Course grouping on the profile (Enrolled vs Completed)

**Decision**: Reuse the MyCourses join verbatim in the Profile page model:
`EnrollmentService.GetMyEnrollmentsAsync(studentId)` +
`ScormAttemptService.GetMyAttemptsAsync(studentId)`. Grouping rule per FR-006: a course
is **Completed** when the user has **at least one** attempt with status `completed` or
`passed`; all other enrollments are **Enrolled**, labeled with the existing
`ScormHelpers.GetDisplayLabel(status)` (status from the latest attempt, or a neutral
"Enrolled" label when no attempt exists). A course appears in exactly one group —
Completed wins (spec edge case: retaking a completed course never loses its completed
status). Rendered server-side as two labeled sections; no HTMX (the spec has no
live-refresh requirement). Per FR-014, a load failure in the course area renders a
friendly error in that area only — personal details still show.

**Rationale**: MyCourses already computes the enrollment×attempt join correctly; the
only delta is the grouping predicate (any-completed vs latest-status), which is
deliberately spec-driven.

**Alternatives considered**:
- HTMX partial like MyCourses — rejected: no refresh requirement in spec; simpler to
  render server-side.
- New cross-module contract for "my course status" — rejected: Host already consumes
  both Application services; a contract would be a new surface for parity (III).

## R7. Making the verification-gate negative branch E2E-observable (SC-002)

**Decision**: Add one **Development-gated** dev page, `/Dev/Unverify`
(`GET ?email=...`), which flips `IsEmailVerified = false` for the matched student and
reports the outcome — the same pattern as the existing `/Dev/Outbox` page
(Development-only, invisible outside dev). The E2E gate spec: sign in as seeded
learner → flip unverified via dev page → attempt name change → assert refusal message +
resend affordance + name unchanged → re-verify (dev outbox link) → sign in → change
succeeds.

**Rationale**: Under spec 027 FR-011, unverified accounts cannot sign in at all, so a
signed-in unverified user is otherwise unreachable through the UI — SC-002 ("100% of
unverified name-change attempts are blocked") would be unverifiable. The gate check
itself (R1) still runs on every save regardless; the dev endpoint only makes the
negative state reachable for tests.

**Alternatives considered**:
- Skip the negative E2E — rejected: SC-002 becomes unverifiable (Constitution XIII).
- Direct DB manipulation from the Node test process — rejected: new SQL client
  dependency in the E2E project, fragile against the app's drop/recreate startup.
- New Host unit-test project for the page model — rejected: a structural addition
  (new project) to test five lines of decision logic, when a dev endpoint reuses an
  established pattern.

## R8. Resend-verification UX on the profile

**Decision**: Mirror the Login page exactly: an `OnPostResendAsync` handler on the
Profile page model calling `RegistrationService.ResendVerificationAsync(email,
baseUrl)` (already throttled to 3/hour per email), rendering the neutral result
message. Shown only when the gate rejects a name change for an unverified account.

**Rationale**: Consistency with the existing unverified-login UX (spec 027) and zero
new service logic.

**Alternatives considered**:
- Redirect to Login — rejected: the user is signed in; the resend must happen in place.

---

## Consolidated decisions (summary)

| # | Decision | One-liner rationale |
|---|----------|---------------------|
| R1 | Page model reuses `IUserProvisioning` + `RegistrationService`; validation in page | No new cross-module surface; matches Settings-page pattern |
| R2 | Re-issue auth cookie after name/photo save (RefreshSignIn) | Cookie embeds Name at login; layout stays claim-driven |
| R3 | New `AvatarPath` cookie claim (URL path), set at sign-in + re-issue | Layout renders avatar with zero DB access |
| R4 | Avatar files in `wwwroot/avatars/` (GUID-keyed) + nullable `Student.AvatarPath` column | Mirrors SCORM-content disk pattern; MSSQL stays relational (VI) |
| R5 | Avatar hidden in Admin view via existing body-class + one CSS rule | Reuses the spec-020 role-view mechanism (Q1 = C) |
| R6 | MyCourses join + "any completed/passed attempt" grouping predicate | Reuses proven join; spec-driven grouping (FR-006) |
| R7 | Development-gated `/Dev/Unverify` page | Makes SC-002's negative branch E2E-observable |
| R8 | In-profile resend handler mirroring Login | Consistency with spec-027 unverified UX |
