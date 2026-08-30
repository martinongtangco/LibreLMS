# Bug Fix Specification: My Courses Progress Track Renders as a Blank Near-Black Bar in Dark Theme

**Feature Branch**: `bug/045-fix-my-courses-progress-track-dark`

**Created**: 2026-08-30

**Status**: Active

**Input**: User bug report (screenshot) — "when you go to My Courses, there's a weird
blank bar underneath the 'Enrolled <date> · 0% complete' message." Screenshot shows
the Dark theme: two course rows (Advanced .NET Patterns, Introduction to C#), each
with the enrollment meta line and, below it, a solid empty dark pill ~200px wide.

## Root Cause

Single layer — a Dark-theme token gap left by spec 042's dark-mode audit.

1. **The bar is the spec 017 progress track.** Spec 017 (Organic UI redesign,
   FR-003) requires each My Courses row to include "an 8px pill progress bar".
   `src/Host/Pages/Shared/_MyCourseRow.cshtml` renders
   `<div class="progress-track"><div class="progress-fill" style="width: {pct}%">`
   and `src/Host/wwwroot/css/site.css` (~line 544) styles
   `.progress-track { background: var(--color-bg); ... }`.

2. **That token means two opposite things per theme.**
   - Light: `--color-bg` is cream `#f6f1e8` — a subtle groove on the near-white
     card surface (`#fdfbf7`). Reads as an intentional empty track.
   - Dark (spec 042, `[data-theme="dark"]` block, site.css ~line 1851):
     `--color-bg` is redefined to `#1d1a16` — the **page canvas**, the darkest
     value in the palette — while the card surface is `#262219`.

3. **Consequence.** In Dark mode the track paints the page-canvas color *on the
   card*, so it reads as a hole punched through the card: a prominent near-black
   pill. Spec 017 FR-004 makes the fill 0% for any course with no SCORM attempt,
   so a freshly-enrolled course shows a solid, content-less dark bar under
   "Enrolled {date} · 0% complete" — the "weird blank bar" in the report.

4. **Why it slipped through.** Spec 042 US3 (T01789) audited `site.css` for
   values that break Dark mode and fixed several stuck-Light tokens
   (see the `bug-042 US3/T018` comments at site.css ~161, ~1105, and the
   `--page-bg`/`--accent`/`--on-accent`/`--border-light` additions in the dark
   block). `.progress-track` uses a *token that is valid in both themes*
   (`var(--color-bg)`) but *wrong for the component in Dark*, so a
   hardcoded-value audit missed it: the token resolves, the value just isn't a
   plausible track color in the dark palette.

## Fix

Give the track its own component token so each theme can state what a track is:

- `:root` (Light): `--color-track: #f6f1e8;` — identical to the current light
  rendering (today's `var(--color-bg)`); **zero visual change in Light**.
- `[data-theme="dark"]`: `--color-track: #211d17;` — a groove between the card
  surface (`#262219`) and the page canvas (`#1d1a16`), so the empty track reads
  as a faint recessed line rather than a near-black bar. The sage fill
  (`--color-accent-2: #9db88f` in Dark) keeps strong contrast on it.
- `.progress-track { background: var(--color-track); }`

**Out of scope**: the 0%-fill behavior itself (spec 017 FR-004, intentional —
"no attempt → 0%"), the meta-line text, and any other dark-theme components
(not audited beyond the reported symptom; if more show up they get their own
bug spec).

## User Scenarios & Testing

### User Story 1 - Dark-mode My Courses rows render a subtle track, not a blank bar (Priority: P1)

**Acceptance Scenarios**:

1. **Given** the learner account with the Dark theme and at least one enrolled
   course, **When** they open `/MyCourses/Index`, **Then** every progress track
   paints the dark groove token (`rgb(33, 29, 23)`) — neither the page canvas
   color nor the card surface color.
2. **Given** the same account with the Light theme, **When** they open
   `/MyCourses/Index`, **Then** the track still paints the light groove
   (`rgb(246, 241, 232)`) — no Light regression from the token split.

**Independent Test**: the new bug-045 tests in
`tests/Playwright.Tests/tests/18-theme-preference.spec.ts` pass, and the full
Playwright suite is green.
