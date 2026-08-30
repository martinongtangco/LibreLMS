# Tasks: Dark-Theme Token for the My Courses Progress Track

**Input**: [plan.md](plan.md), [spec.md](spec.md)

## Phase 1: Setup

^- [X] T00[1-5] Create branch `bug/045-fix-my-courses-progress-track-dark` from `master` and confirm `git branch --show-current` reports it (Principle VIII)

## Phase 2: Fix

^- [X] T00[1-5] In `src/Host/wwwroot/css/site.css`: add `--color-track: #f6f1e8;` to the `:root` block, change `.progress-track` to `background: var(--color-track)`, and add `--color-track: #211d17;` (with the bug-045 comment) to the `[data-theme="dark"]` block
^- [X] T00[1-5] In `tests/Playwright.Tests/tests/18-theme-preference.spec.ts`: add the Dark pin (US3 block: track = `rgb(33, 29, 23)`, ≠ body bg, ≠ card bg on `/MyCourses/Index`) and the Light pin (US2 block: track = `rgb(246, 241, 232)`), house pattern (`loginAs` → `setTheme` → `restoreSystemTheme` in `finally`)

## Phase 3: Verification (Principle XIII)

^- [X] T00[1-5] Rebuild + restart the app in the devcontainer (`scripts/restart-app.sh --no-docker --background`), show build output + "Now listening" + 200 from `/Courses/Index`
^- [X] T00[1-5] Run `npx playwright test tests/18-theme-preference.spec.ts` from `tests/Playwright.Tests` and capture passing output (both bug-045 pins)
^- [X] T00[1-5] Run the FULL `npx playwright test` suite and capture passing output (note any transient flakes with re-run evidence)

## Phase 4: Merge

^- [X] T006 Merge `bug/045-fix-my-courses-progress-track-dark` into `master`, then on `master` rebuild + restart + re-run the full `npx playwright test` (Principle XIII gate 3 — post-merge regression), update this spec's Status to Complete, and leave `master` clean (Principle XII)
