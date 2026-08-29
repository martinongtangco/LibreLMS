# UI Contract: Theme Preference

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-29

The user-facing contracts this feature exposes: (1) the page-level theme rendering
contract and (2) the theme save endpoint contract.

## 1. Page rendering contract (every application page)

**Scope**: every page rendered through `_Layout.cshtml` — public catalog, course
detail, My Courses, profile, Settings, admin pages, SCORM launch shell.

### HTML contract

- **Signed in, preference `Light`**: `<html lang="en" data-theme="light">`
- **Signed in, preference `Dark`**: `<html lang="en" data-theme="dark">`
- **Signed in, preference `System`**: no `data-theme` attribute in the server-rendered
  markup; an **inline `<script>` in `<head>` before the `site.css` link** sets
  `data-theme="light"` or `data-theme="dark"` from
  `prefers-color-scheme` before first paint, and follows subsequent device changes live
  (attribute updates without reload).
- **Anonymous**: identical to `System` — no attribute server-side; same inline script
  resolves it.

**Contract guarantees** (assertable in E2E):
1. The correct theme is in effect from first paint — no flash (SC-004).
2. `data-theme` is either absent (System, resolved pre-paint), `"light"`, or `"dark"` —
   never any other value.
3. Changing the device light/dark setting while a System-mode page is open updates the
   attribute within one second, without reload (SC-005).
4. SCORM launch page: the attribute themes the shell only; the iframe's document is
   unthemed (FR-012).

### Visual contract

| Surface | Light (paper) | Dark (night) |
|---------|---------------|--------------|
| Page background | warm paper `#f6f1e8` | soft warm dark `#1d1a16` |
| Card/surface | `#fdfbf7` — **never pure white** (SC-006) | `#262219` |
| Body text | `#2c2a26` (12.7:1 on bg) | `#e9e4da` (13.7:1 on bg) |
| Secondary/muted text | `#6b6558` (≥ 5.1:1) | `#a49c8e` (≥ 5.8:1) |
| Brand / success / error | AA on surfaces (research R3 table) | AA on surfaces |

Both palettes: WCAG AA ≥ 4.5:1 for body/secondary text (SC-003).

## 2. Theme save endpoint contract

**Endpoint**: `POST /Account/Settings?handler=Theme`

**Access**: authenticated users only (page is `[Authorize]`). Anti-forgery token is
required and validated implicitly — sent in the form body (`__RequestVerificationToken`
from the rendered Settings form). No `.DisableAntiforgery()`.

**Request** (`application/x-www-form-urlencoded`, from `FormData` of `#settings-form`):

| Field | Type | Notes |
|-------|------|-------|
| `ThemePreference` | `string` | `System` \| `Light` \| `Dark`; unknown → normalized to `System` |
| `EmailNotificationsEnabled` | `bool` | current toggle state (carried so the save cannot clobber it) |
| `__RequestVerificationToken` | `string` | anti-forgery, from the rendered form's hidden field |

Header: `X-Requested-With: fetch` (client marker for the JSON response; a plain browser
form POST to the same handler still works and gets the standard page response).

**Response** (AJAX): `200 OK`, `application/json`

| Field | Type | Notes |
|-------|------|-------|
| `success` | `bool` | `true` only when the preference was persisted **and** the cookie re-issued |
| `message` | `string?` | user-facing message when `success` is `false` |

**Side effects on success**:
1. `Student.ThemePreference` updated in MSSQL (system of record).
2. Auth cookie re-issued with the new `ThemePreference` claim (`AuthCookieRefresher`).
3. The next rendered page (including the re-render of the current one, if any) carries
   the new theme; the client additionally applies `data-theme` immediately.

**Failure modes**:
| Condition | Response |
|-----------|----------|
| Server error persisting the preference | `200` + `{ "success": false, "message": "..." }` — displayed theme MUST remain unchanged (FR-011) |
| Unauthenticated | standard 302 to login (framework behavior) |
| Invalid anti-forgery token | framework 400 |

## 3. Settings page control contract

- The Theme `<select>` (values `System`/`Light`/`Dark`) reflects the stored preference on
  page load (unknown/empty stored value renders as `System` selected — FR-010).
- Changing the select saves immediately (AJAX per contract 2) — no full-page navigation,
  no loss of in-progress form input (FR-003); a confirmation alert appears on success,
  an error alert on failure.
- The Email Notifications toggle retains its existing behavior (plain form POST).
