# Plan: Dark-Theme Token for the My Courses Progress Track

**Input**: [spec.md](spec.md)

## Summary

Introduce a `--color-track` component token (Light `#f6f1e8` = today's rendering;
Dark `#211d17` = subtle groove between card surface and page canvas) and point
`.progress-track` at it. Add bug-045 E2E pins to the existing theme test file.

## Technical Approach

- **File 1**: `src/Host/wwwroot/css/site.css`
  - `:root` block: add `--color-track: #f6f1e8;` next to `--color-bg`/`--color-surface`.
  - `.progress-track` (~line 544): `background: var(--color-bg)` →
    `background: var(--color-track)`.
  - `[data-theme="dark"]` block (~line 1851): add
    `--color-track: #211d17; /* My Courses progress track: was --color-bg (the
    page canvas) — a near-black blank pill on cards (bug-045). Groove between
    surface #262219 and canvas #1d1a16. */`
  - Light value equals the current Light rendering exactly, so Light is
    unchanged by construction.
- **File 2**: `tests/Playwright.Tests/tests/18-theme-preference.spec.ts`
  - US3 Dark block: new test — Dark + `/MyCourses/Index`: first `.progress-track`
    computed background is `rgb(33, 29, 23)`, and is neither the body (canvas)
    background nor the card background (the regression was track == canvas).
  - US2 Light block: new test — Light + `/MyCourses/Index`: track background is
    `rgb(246, 241, 232)` (no Light regression from the token split).
  - Both tests follow the house pattern: `authFixture.loginAs(page, 'Learner')`,
    `setTheme(...)`, `restoreSystemTheme(page)` in `finally` (file is a top-level
    serial bundle — no extra configuration needed).

- **Why a token, not a `[data-theme="dark"] .progress-track` override**: the
  site is token-driven (spec 017 research §1) and the spec 042 dark block is a
  token block; the US3/T018 fixes there added missing *tokens*, not scattered
  overrides. A named `--color-track` states the component's intent and keeps
  both themes in one place.

## Verification (Principle XIII)

1. Rebuild + restart the Host app in the devcontainer
   (`scripts/restart-app.sh --no-docker --background`); show build output +
   "Now listening" + HTTP 200 from `/Courses/Index`.
2. `npx playwright test tests/18-theme-preference.spec.ts` from
   `tests/Playwright.Tests` → all pass (includes the two new bug-045 pins).
3. Full `npx playwright test` → green.
4. Post-merge (gate 3): on `master` rebuild + restart + full `npx playwright
   test` → green.

## Risks

- None behavioral: pure presentation token; Light rendering is byte-identical
  in effect (`#f6f1e8` both before and after).
- `.progress-track` is used only by `_MyCourseRow.cshtml` (My Courses rows) —
  grepped; no other consumer.
