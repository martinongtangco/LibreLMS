# Research: Per-User Theme Preference (System / Light / Dark)

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-29

All Technical Context unknowns resolved. No NEEDS CLARIFICATION items remain.

---

## R1: How the saved theme reaches every page's render

**Decision**: Carry the preference in the auth cookie as an **always-present
`ThemePreference` claim** (values `System` | `Light` | `Dark`, default `System`), read
claim-only in `_Layout.cshtml` — the exact pattern spec 030 established for `AvatarPath`.
On theme save, `AuthCookieRefresher.RefreshAsync` re-issues the cookie so the re-rendered
page and all later requests carry the new value.

**Rationale**:
- `_Layout.cshtml` has a documented house convention: "no service injection / DB access
  in the layout" (avatar comment, spec 030 R3). A per-request `IActionFilter` doing a DB
  lookup would break that convention and add a query to every request.
- `AuthClaims.Build` is already the pinned single source of truth for the claim set
  (bug-039 history), and `AuthCookieRefresher` exists precisely to re-issue claims after
  account changes. Theme preference is the same shape as `AvatarPath`: low-churn,
  account-scoped, saved through one page.
- Anonymous visitors have no cookie → no claim → System (FR-009) falls out for free.

**Alternatives considered**:
- *Per-request `IAsyncActionFilter` resolving the preference from the DB* — always fresh,
  but a DB hit on every page load and DB access leaking into the render pipeline; rejected.
- *Server-side session/HttpContext cache populated by an event* — more moving parts for a
  value that only changes through one form; rejected (Principle II: no unexplained
  abstraction).
- *Client-side localStorage mirroring* — would survive browser clearing failures
  incorrectly (stale theme after account change on another device); the account is the
  source of truth; rejected as the persistence mechanism (it may still be used for
  nothing — single mechanism preferred).

## R2: No flash of the wrong theme + live System tracking

**Decision**: Server renders `<html lang="en" data-theme="light"|"dark">` for explicit
preferences. For `System` (and anonymous), the server renders **no attribute**; a ~10-line
**inline `<script>` in `<head>`, before the `site.css` link**, reads
`matchMedia('(prefers-color-scheme: dark)')`, sets `document.documentElement.dataset.theme`,
and subscribes to its `change` event to live-follow device changes (FR-007/SC-005).

**Rationale**:
- Inline head script executes before first paint → correct theme on the very first frame
  (FR-008/SC-004). An external JS file would paint at least one frame unstyled-wrong.
- CSS is attribute-driven (`[data-theme="dark"]` token block), so both the inline script
  (System) and server rendering (Light/Dark) converge on the same mechanism.
- `matchMedia` `change` events give live OS following with zero polling (FR-007).
- Browsers without `prefers-color-scheme` (effectively none in scope) → script falls back
  to `light` (documented assumption in spec).

**Alternatives considered**:
- *Pure-CSS `@media (prefers-color-scheme: dark)` for System* — no JS for the initial
  state, but live-following an explicit `Dark` saved while device is light still needs the
  attribute mechanism, so two systems would coexist; rejected in favor of one mechanism.
- *Setting the attribute in a bottom-of-body script* — risks one painted frame in the
  wrong theme; rejected.

## R3: Palettes — warm paper Light, night-reading Dark, WCAG AA verified

**Decision**: Keep the existing `:root` token set as the **Light** palette with paper
adjustments (background stays warm `#f6f1e8`, **surface moves from pure `#ffffff` to
`#fdfbf7`**), and add a `[data-theme="dark"]` block overriding all 20 color tokens.
Every body/secondary/semantic pair was contrast-computed (relative-luminance formula,
WCAG 2.x) and passes AA ≥ 4.5:1:

| Pair | Light (fg on bg) | Ratio | Dark (fg on bg) | Ratio |
|------|------------------|-------|-----------------|-------|
| text / bg | `#2c2a26` on `#f6f1e8` | 12.73:1 | `#e9e4da` on `#1d1a16` | 13.68:1 |
| text / surface | `#2c2a26` on `#fdfbf7` | 13.86:1 | `#e9e4da` on `#262219` | 12.51:1 |
| muted / bg | `#6b6558` on `#f6f1e8` | 5.15:1 | `#a49c8e` on `#1d1a16` | 6.38:1 |
| muted / surface | `#6b6558` on `#fdfbf7` | 5.60:1 | `#a49c8e` on `#262219` | 5.83:1 |
| brand / surface | `#b0522f` on `#fdfbf7` | 4.96:1 | `#d98a63` on `#262219` | 5.86:1 |
| success / surface | `#557a3a` on `#fdfbf7` | 4.80:1 | `#a3c585` on `#262219` | 8.21:1 |
| error / surface | `#c62828` on `#fdfbf7` | 5.44:1 | `#e57373` on `#262219` | 5.31:1 |

Dark palette (full token set, warm-neutral "soft dark", not pure black):
`bg #1d1a16`, `surface #262219`, `text #e9e4da`, `text-muted #a49c8e`, `border #3a342b`,
`border-strong #4a4337`, `brand #d98a63`, `brand-hover #e29a75`, `accent-2 #9db88f`,
`category-bg #3a3025` / `category-text #d9b48f`, `duration-bg #2f3a26` /
`duration-text #b5cf9a`, `success-bg #2f3a26` / `success-text #b5cf9a`,
`error-bg #3a2626` / `error-border #e57373`, plus matching `text-faint` / `accent-700`
values. Badge *backgrounds* in dark are muted (low-luminance) with light badge *text* —
the inverted relationship of light mode.

**Rationale**: The user asked for paper-like Light and balanced night-reading Dark; both
palettes are warm-toned (matching the existing "Organic" design system, spec 017) so the
themes feel like the same product. Dark avoids pure black (halation/reduced perceived
contrast for text) and pure-white text (glare).

**Alternatives considered**:
- *Pure CSS-variable-free theming (separate stylesheet per theme)* — duplicates ~1800 lines; rejected.
- *HSL lightness-inversion of the light palette* — produces muddy brand/semantic colors and unverified contrast; rejected.
- *Cool gray dark palette* — clashes with the warm organic identity; rejected.

**Note**: `--color-text-faint` is decorative (placeholders, helper hints) and is below
4.5:1 in the *current* light palette already (`#9e9e9e` on `#faf8f4` ≈ 2.8:1) — out of
scope for FR-004/FR-005 ("body and secondary text"). Light value unchanged (no regression
introduced); dark value chosen comfortably (`~4.5:1` on surface).

## R4: Saving the theme without a full-page reload (FR-003, FR-011)

**Decision**: The theme `<select>` stops using `form.requestSubmit()`. On `change`, a
small inline script on the Settings page:
1. Builds `FormData` from the existing `#settings-form` (carries both controls' current
   DOM state, including the anti-forgery hidden field).
2. `fetch`-POSTs to `/Account/Settings?handler=Theme` with
   `X-Requested-With: fetch`.
3. Server-side `OnPostThemeAsync` (new handler, anti-forgery validated implicitly — token
   travels in the form body) updates the preference, re-issues the cookie claim via
   `AuthCookieRefresher`, and returns **JSON** `{ success, message }` for AJAX requests
   (and a redirect for non-AJAX fallback).
4. On `success` the script applies `document.documentElement.dataset.theme` client-side
   (instant, no navigation — in-progress form input elsewhere on the page is untouched)
   and renders the existing success alert; on failure it renders the error alert and
   leaves the displayed theme unchanged.

The email-notifications toggle keeps its existing plain `requestSubmit()` behavior
(out of scope; the fetch payload simply includes its current state, so a theme save can
never clobber it).

**Rationale**:
- `fetch` + `FormData` reuses the form's own hidden anti-forgery token — ASP.NET Core
  validates it from the form body, so no new token plumbing and no
  `.DisableAntiforgery()` (cf. spec 024's anti-forgery history).
- A dedicated `?handler=Theme` returning JSON keeps `OnPostAsync` (used by the email
  toggle's full-form POST) untouched and makes the save path independently testable.
- Applying the attribute client-side after a confirmed save satisfies FR-011 strictly
  (failed save ⇒ theme unchanged).

**Alternatives considered**:
- *Keep plain form POST re-render* — zero new client code, but every save is a full page
  re-render (violates the "without a full-page reload / without discarding in-progress
  form input" reading of FR-003); rejected.
- *htmx `hx-post` + `hx-swap="none"` + `hx-on::after-request`* — consistent with the
  enroll flow, but applying the theme needs htmx request lifecycle events plus a
  success/error response body convention; the ~20-line fetch is plainer for one control
  (Principle IV: plainest readable option).

## R5: SCORM authored content stays unthemed (FR-012)

**Decision**: No work required — `Pages/Scorm/Launch.cshtml` renders authored content in
an **iframe**; `data-theme` and token overrides on the parent document do not leak into
the iframe's own document. The shell chrome (nav/header/footer) themes normally.

**Rationale**: Iframe documents have their own CSS scope; the SCORM shim and content
stylesheets are untouched. Verified by E2E (theme asserted on the shell, not on iframe
content).

**Alternatives considered**: *CSS `all: revert`/shadow tricks to re-theme authored
content* — out of scope per spec assumption; could break authoring-tool output.

## R6: Claim-set pinning and cookie size

**Decision**: Add `ThemePreference` as an **always-present** claim to
`AuthClaims.Build` (default `"System"` when the student row is missing/empty — FR-010
fallback lives in `AuthClaims`/layout, both treating unknown values as System). Update
`tests/Host.Tests/AuthClaimsTests.cs` (the pinned claim-set test) in the same change.

**Rationale**: Cookie payload grows by ~30 bytes — negligible against the ~4KB limit
(current claims already carry OrganizationId + SecurityStamp GUIDs). "Always present"
simplifies the layout (no null-branch) and makes the default explicit and testable.

**Alternatives considered**: *Present-only-when-non-default claim* (like `AvatarPath`) —
slightly smaller cookie, but the layout then needs a missing-claim branch for the default
case; the value is 6 bytes, simplicity wins.

## R7: ADR

**Decision**: Record the R1 storage decision (theme in auth cookie claim, re-issued on
save) as `docs/adr/0009-theme-preference-in-auth-claim.md` (context → decision →
consequences). Next free number in `docs/adr/` (highest is `0008`).

**Rationale**: Principle IV — a non-obvious structural/storage decision that a future
reader will ask "why not just query the DB?".

---

## Consolidated decision summary

| # | Decision |
|---|----------|
| R1 | Theme travels as an always-present auth cookie claim; layout stays claim-only; cookie re-issued on save via existing `AuthCookieRefresher` |
| R2 | `data-theme` attribute on `<html>`; inline head script resolves System before first paint + live-follows device changes |
| R3 | Paper Light (`surface #fdfbf7`, no pure white) + soft warm Dark (`bg #1d1a16`); all AA-critical pairs computed ≥ 4.5:1 |
| R4 | Theme select saves via `fetch` POST to `?handler=Theme` → JSON; client applies theme only on confirmed success |
| R5 | SCORM iframe isolates authored content — no theming work |
| R6 | Claim always present, default `System`; `AuthClaimsTests` pin updated in same change |
| R7 | ADR 0009 documents the claim-storage decision |
