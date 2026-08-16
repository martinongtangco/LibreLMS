# ADR-0007: User Avatar Storage & Display

**Status**: Accepted
**Date**: 2026-08-16
**Supersedes**: N/A

## Context

Spec 030 adds a self-service display photo to the profile. Three constraints shape the design:

- **Constitution VI** requires durable relational state in MSSQL (the system of record) and asks, of anything stored elsewhere, "would losing this on a flush actually be fine?"
- The app already stores durable, file-shaped content on disk under `wwwroot` (SCORM course content via `ScormPackageService`) — an established precedent for "durable but not relational" data.
- The upper-right nav (shared `_Layout`) must show the photo for every page render. Injecting a service or doing an async DB lookup in a shared layout would add a query to every page and duplicate the claim mechanism the app already uses for the display name and organization.

## Decision

### Storage: files on disk, GUID-keyed; the URL path in MSSQL

Avatar image bytes live on disk under `src/Host/wwwroot/avatars/`, served by the existing static-files middleware (no new endpoint). Filenames are `{studentId-lower}{extension}` — the student id comes from the auth cookie claim, **never** from user input, so there is no path-traversal or filename-enumeration surface and one file per user+extension. `Student` gains one nullable column `AvatarPath` (NVARCHAR(200)) holding the URL path (e.g. `/avatars/3f2c….png`); null means "no photo" (the UI renders an initials placeholder). The column — not the disk file — is the system of record.

**Rejected alternatives**: a BLOB column would bloat the relational store with non-relational bytes (VI); Valkey fails the "would losing this be fine?" test (an avatar is durable user content) and is reserved for SCORM runtime state; an authenticated download endpoint (`/avatars/download?id=…`) would add per-request auth + a DB hit for a decorative image.

### Display: one cookie claim + cookie re-issue

One new cookie claim, `AvatarClaimTypes.AvatarPath`, carries the URL path. It is set at sign-in alongside the existing claims and re-issued after each successful profile change (name or photo save) via `AuthCookieRefresher` (the ASP.NET Core "RefreshSignIn" pattern, rebuilding the identical claim list from the fresh `Student` row). The layout renders `<img class="account-avatar">` from the claim — or an initials placeholder when absent — with **zero** service injection or DB access. Nav visibility for admin-role users (Q1 = option C: hidden in the Admin view) is one CSS rule on the body class the existing role-pill JS already toggles (`.role-admin .account-avatar { display: none; }`) — no new JavaScript.

## Consequences

**Positive**:
- The layout stays 100% claim-driven, consistent with how `OrganizationId` already flows; no per-request DB cost
- GUID-keyed filenames are unguessable and collision-free; one file per user+extension keeps replacement trivial (write temp → move → delete the replaced file)
- All durable relational state (the URL column) stays in MSSQL per VI; disk holds the file-shaped bytes, mirroring the SCORM content pattern
- The `AvatarPath` column is nullable and additive — one small migration, no data backfill

**Negative**:
- Avatar URLs are effectively **public** to anyone who knows them (no per-request auth). Accepted trade-off: the names are unguessable GUIDs, and a display photo is not confidential data in this teaching project — the trade-off is recorded here rather than hidden
- Avatar files persist in the `wwwroot` bind mount across app restarts and are gitignored (runtime data, not source)
- A DB failure after the file move can orphan a new file; the handler deletes it best-effort and leaves the previous photo untouched (FR-010)

## Related

- Spec 030 (editable user profile) — research R2 (cookie re-issue), R3 (avatar claim), R4 (disk storage), R5 (admin-view visibility)
- ADR-0003 (polyglot storage) — the "would losing this be fine?" test applied to avatars
