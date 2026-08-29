# Tasks: Per-User Theme Preference (System / Light / Dark)

**Input**: Design documents from `/specs/042-user-theme-preference/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/theme-ui.md, quickstart.md

**Tests**: E2E test tasks are included — mandated by Constitution Principle XIII
("If no test covers the changed behavior, write one"), and the spec's Success Criteria
are browser-observable.

**Organization**: Tasks are grouped by user story to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g. US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Single modular monolith: `src/Host` (composition root), `src/Modules/*`,
  `tests/*` at repository root (per plan.md Project Structure)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Branch hygiene per Constitution Principle VIII (no task can edit files
before this completes — the "Before You Touch Code" gate)

- [X] T001 Create branch `story/042-user-theme-preference` from `master` and confirm `git branch --show-current` reports it (run from repo root `/workspace`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Theme must be able to (a) travel from the account to every page render and
(b) be expressed in CSS, before any user story is testable. No story work begins until
this phase is complete.

- [X] T002 [P] Add `ThemePreference` claim type constant (new file `src/Host/ManagementAuth/ThemeClaimTypes.cs`, mirroring `AvatarClaimTypes.cs`) and extend `AuthClaims.Build` in `src/Host/ManagementAuth/AuthClaims.cs` with an always-present `ThemePreference` claim: new optional parameter `string? themePreference = null`, normalized so null/empty/unknown values become `"System"` (FR-010); update the class doc comment's claim list
- [X] T003 [P] Add `ThemePreference` field (default `"System"`) to the `StudentProvisionedDto` record in `src/Modules/Enrollment.Contracts/IUserProvisioning.cs` and map it in `UserProvisioningService.ToDto` (normalize null/empty → `"System"` there so the DTO never carries garbage) in `src/Modules/Enrollment/Application/UserProvisioningService.cs`
- [X] T004 [P] Apply both palettes to `src/Host/wwwroot/css/site.css` per research.md R3: adjust the `:root` Light tokens to paper values (`--color-bg: #f6f1e8`, `--color-surface: #fdfbf7`, `--color-text: #2c2a26`, `--color-text-muted: #6b6558`, `--color-brand: #b0522f`, `--color-success-text/--color-duration-text: #557a3a`, keeping all other light values) and add a new `[data-theme="dark"] { ... }` block overriding ALL 20 color tokens with the R3 dark values (bg `#1d1a16`, surface `#262219`, text `#e9e4da`, muted `#a49c8e`, border `#3a342b`, brand `#d98a63`, badge/semantic pairs as listed in research.md R3); typography/spacing/layout tokens stay untouched
- [X] T005 Update the claim-set pin test in `tests/Host.Tests/AuthClaimsTests.cs` to assert the always-present `ThemePreference` claim (default case → `"System"`, explicit values pass through, null/empty/unknown → `"System"`); run `dotnet test tests/Host.Tests` (depends on T002)
- [X] T006 Wire the theme through both sign-in paths: pass `student.ThemePreference` into `AuthClaims.Build` in `src/Host/Pages/Account/Login.cshtml.cs` (OnPostAsync, ~line 102) and pass `student.ThemePreference` from the DTO in `src/Host/ManagementAuth/AuthCookieRefresher.cs` RefreshAsync; add a doc-comment note that the claim is re-issued on theme save (depends on T002, T003)
- [X] T007 Create EF migration `AddThemePreferenceToAdminListLearnersProcedure` in `src/Host/Migrations/Enrollment/` that runs `ALTER PROCEDURE AdminListLearners` adding `ThemePreference` (from `Students`) to the learner-row SELECT, and read the new column (ordinal 8, `DBNull` → `"System"`) in the `ListAsync` reader in `src/Modules/Enrollment/Application/UserProvisioningService.cs` (update the "columns 0..7" comment); run `dotnet build src/Host` to confirm migration generation compiles (depends on T003)
- [X] T008 In `src/Host/Pages/Shared/_Layout.cshtml`: (1) render `<html lang="en" data-theme="light">` / `data-theme="dark"` when the `ThemePreference` claim is Light/Dark, and NO `data-theme` attribute when the claim is System, missing (anonymous), or unknown; (2) add an inline `<script>` in `<head>` BEFORE the `site.css` link that, only when no attribute is present, sets `document.documentElement.dataset.theme` from `matchMedia('(prefers-color-scheme: dark)')` (fallback to `"light"` if `matchMedia` is missing) and subscribes to its `change` event to live-update the attribute (FR-007/FR-008; contracts/theme-ui.md §1) (depends on T002)
- [X] T009 [P] Write ADR `docs/adr/0009-theme-preference-in-auth-claim.md` (context → decision → consequences, one page) documenting research.md R1: theme carried as an always-present auth cookie claim re-issued on save, vs. the rejected per-request DB filter (Principle IV)

**Checkpoint**: Foundation ready — a signed-in user's theme claim drives `<html data-theme>`
on every page (Light/Dark visible via the new palettes; System follows the device). The
Settings save path still uses the old full-form POST (DB updates, cookie stale) — that is
what US1 completes. Verify: `dotnet build src/Host` + `dotnet test tests/Host.Tests` pass,
restart the app, sign in, and confirm the correct attribute renders.

---

## Phase 3: User Story 1 - Choose a theme that applies immediately and persists (Priority: P1) 🎯 MVP

**Goal**: The Settings Theme selector saves via a no-reload AJAX POST, re-issues the
cookie claim, and applies the theme client-side on success — the choice then survives
every page, browser restart, and sign-out/sign-in (FR-001…FR-003, FR-011).

**Independent Test**: Sign in → Settings → select Dark → page re-skins without any
navigation → navigate to Browse Courses (still dark) → sign out, close browser, sign in
again → dark restored, no re-selection.

### Tests for User Story 1 (write FIRST, must FAIL before T011/T012) ⚠️

- [X] T010 [P] [US1] Create `tests/Playwright.Tests/tests/18-theme-preference.spec.ts` with US1 scenarios (follow existing spec conventions, e.g. `12-profile-name.spec.ts`, reusing `tests/Playwright.Tests/fixtures/authFixture.ts`): (a) Settings loads with `System` selected for a fresh account; (b) selecting `Dark` triggers NO page navigation (assert `page.waitForEvent('framenavigated')` does not fire / URL unchanged) and `<html>` gains `data-theme="dark"`; (c) navigating to `/Courses/Index` keeps `data-theme="dark"`; (d) sign out → close context → sign in again → `/Account/Settings` shows `Dark` selected and `<html>` is dark (FR-002/FR-003, SC-001/SC-002)

### Implementation for User Story 1

- [X] T011 [US1] Add `OnPostThemeAsync` handler to `src/Host/Pages/Account/Settings.cshtml.cs`: bind `ThemePreference` + `EmailNotificationsEnabled` from the form; normalize unknown values to `"System"`; call `EnrollmentService.UpdatePreferencesAsync`; fetch the current account via injected `IUserProvisioning.GetByIdAsync` (same seam Profile.cshtml.cs uses) and re-issue the cookie via injected `AuthCookieRefresher.RefreshAsync`; when `Request.Headers["X-Requested-With"] == "fetch"` return `Json(new { success, message })` (200, `success=false` + message on any exception — FR-011), otherwise fall back to the existing page-re-render behavior; do NOT disable anti-forgery (the token arrives in the form body)
- [X] T012 [US1] In `src/Host/Pages/Account/Settings.cshtml`: replace the theme `<select>`'s `onchange="…requestSubmit()"` with a small inline script that on `change` builds `FormData` from `#settings-form` and `fetch`-POSTs to `/Account/Settings?handler=Theme` with header `X-Requested-With: fetch`; on `success` it sets `document.documentElement.dataset.theme` to the chosen value (map `System` → remove attribute, re-run the head resolver's logic for the current device setting) and shows the existing success alert; on failure shows the error alert and leaves the displayed theme unchanged (FR-003/FR-011); the Email Notifications toggle keeps its plain `requestSubmit()` untouched
- [X] T013 [US1] Rebuild + restart the app (`rm -rf src/Host/obj src/Host/bin && dotnet build src/Host`, relaunch per quickstart.md — Razor views are precompiled), run `npx playwright test tests/18-theme-preference.spec.ts` from `tests/Playwright.Tests` and capture passing output (Principle XIII gates 1+2 evidence for US1)

**Checkpoint**: User Story 1 fully functional — theme selection is end-to-end: save,
instant apply, persistence across pages and re-login. **This is the MVP.**

---

## Phase 4: User Story 2 - A light theme that is easy on the eyes (Priority: P2)

**Goal**: Light mode is verifiably paper-like — no pure-white background or surface on
standard pages, body/secondary text AA (FR-004, SC-003, SC-006).

**Independent Test**: With Light active, computed styles on the standard page set show
warm paper tones (no `rgb(255, 255, 255)` backgrounds) and body/muted text ≥ 4.5:1
contrast.

### Tests for User Story 2 (write FIRST, must FAIL before T015) ⚠️

- [X] T014 [P] [US2] Extend `tests/Playwright.Tests/tests/18-theme-preference.spec.ts` with US2 scenarios: with Light active, for each page in {`/Courses/Index`, `/Courses/Detail` (first seeded course), `/MyCourses/Index`, `/Account/Settings`}: `getComputedStyle(document.body).backgroundColor` and `.card`'s computed background are NOT `rgb(255, 255, 255)` (SC-006); computed body-text and muted-text colors vs. their backgrounds compute ≥ 4.5:1 relative-luminance ratio in-test (SC-003)

### Implementation for User Story 2

- [X] T015 [US2] Audit `src/Host/wwwroot/css/site.css` (and inline styles in `src/Host/Pages/**/*.cshtml`) for hardcoded colors that bypass the Light tokens on standard pages — any literal `#ffffff`/`#fff` backgrounds or surfaces → replace with `var(--color-surface)`/`var(--color-bg)` or a paper token; confirm the rendered light palette matches research.md R3 (depends on T004)
- [X] T016 [US2] Rebuild + restart, run the US2 scenarios in `tests/Playwright.Tests/tests/18-theme-preference.spec.ts` and capture passing output

**Checkpoint**: Stories 1 AND 2 independently functional — Dark/Light selection works and
Light is verified paper-like with AA text contrast.

---

## Phase 5: User Story 3 - A dark theme balanced for night reading (Priority: P2)

**Goal**: Dark mode is verifiably comfortable — soft (non-black) backgrounds, AA body/
secondary/semantic contrast, distinguishable controls (FR-005, SC-003).

**Independent Test**: With Dark active, computed styles show `bg #1d1a16`-class soft dark
(never `rgb(0, 0, 0)`), body/muted/brand/success/error all ≥ 4.5:1 on their surfaces.

### Tests for User Story 3 (write FIRST, must FAIL before T018) ⚠️

- [ ] T017 [P] [US3] Extend `tests/Playwright.Tests/tests/18-theme-preference.spec.ts` with US3 scenarios: with Dark active on `/Courses/Index` + `/Account/Settings`: computed body background is not `rgb(0, 0, 0)`; body text, muted text, and (when present) brand/success/error element colors each compute ≥ 4.5:1 against their computed backgrounds in-test (SC-003); badge/alert background colors differ from surface (distinguishability spot-check)

### Implementation for User Story 3

- [ ] T018 [US3] Audit `src/Host/wwwroot/css/site.css` (+ `src/Host/Pages/**/*.cshtml` inline styles) for hardcoded values that break Dark mode — literal white/light backgrounds or text, icon stroke colors (Lucide `<i>` inherit `currentColor` — verify nav/brand icons), focus outlines, shadows, `color-scheme` — fix by tokenizing or adding `[data-theme="dark"]` overrides so the R3 dark palette is what renders (depends on T004; runs after T015 to avoid same-file conflicts)
- [ ] T019 [US3] Rebuild + restart, run the US3 scenarios in `tests/Playwright.Tests/tests/18-theme-preference.spec.ts` and capture passing output

**Checkpoint**: Stories 1–3 independently functional — both palettes are quality-verified.

---

## Phase 6: User Story 4 - System mode follows my device, with no flash (Priority: P3)

**Goal**: System (the default) matches the device setting, follows live changes without
reload, applies from first paint, and is what anonymous visitors get (FR-007…FR-009,
SC-004, SC-005).

**Independent Test**: With System selected: browser dark → app dark; flip the device
setting with a page open → app follows < 1s, no reload; fresh page load → correct theme
from first paint; incognito (anonymous) → follows device.

### Tests for User Story 4 (write FIRST, must FAIL before T021) ⚠️

- [ ] T020 [P] [US4] Extend `tests/Playwright.Tests/tests/18-theme-preference.spec.ts` with US4 scenarios using Playwright `colorScheme` emulation: (a) no-flash — intercept the FIRST response's HTML of a page load and assert the theme state is correct pre-settle (Light/Dark: `data-theme` present in served HTML; System: inline head script present in `<head>` before the `site.css` link); (b) live follow — with System active and `colorScheme: 'light'`, set `colorScheme: 'dark'` via `page.emulateMedia` and assert `<html>` attribute becomes `dark` with no reload/navigation; (c) anonymous — unauthenticated context with `colorScheme: 'dark'` browses `/Courses/Index` → attribute `dark`; with `light` → `light` (FR-007…FR-009)

### Implementation for User Story 4

- [ ] T021 [US4] Verify and, if gaps exist, fix the head inline script in `src/Host/Pages/Shared/_Layout.cshtml`: `matchMedia('(prefers-color-scheme: dark)').addEventListener('change', …)` live-follow, the no-`matchMedia` fallback to `light`, and that the attribute is removed (not stale) when a signed-in user switches System↔explicit (T012's apply path) — contracts/theme-ui.md §1 guarantees (depends on T008)
- [ ] T022 [US4] Rebuild + restart, run the US4 scenarios in `tests/Playwright.Tests/tests/18-theme-preference.spec.ts` and capture passing output

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Full regression evidence per Principle XIII and the quickstart walkthrough

- [ ] T023 Run the complete verification gates from quickstart.md: `dotnet test tests/ArchitectureTests` (Principle III), `dotnet test tests/Host.Tests`, `npx playwright test` (FULL suite from `tests/Playwright.Tests`) — capture all passing output; fix any regressions the full run exposes
- [ ] T024 Complete the 8 manual scenarios in quickstart.md "Manual validation" in a real browser at `http://localhost:5000` (default System, Dark, Light paper, persistence, live-follow, anonymous, SCORM iframe isolation, save-failure) and record results in the PR/commit notes
- [ ] T025 Merge `story/042-user-theme-preference` into `master`, then on `master` rebuild + restart + re-run `npx playwright test` (Principle XIII gate 3 — post-merge regression), and switch back to `master` clean (Principle XII)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — runs first (branch gate)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phases 3–6)**: All depend on Foundational; proceed sequentially
  P1 → P2 → P2 → P3 (they share `site.css` and `18-theme-preference.spec.ts`, so
  story-to-story parallelism would conflict on those files)
- **Polish (Phase 7)**: Depends on all user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational — no dependency on other stories (MVP)
- **US2 (P2)**: After US1 — same spec file & site.css, no functional dependency
- **US3 (P2)**: After US2 (same-file ordering with T015)
- **US4 (P3)**: After US3 (verifies the head script US1's apply path interacts with)

### Within Each User Story

- E2E scenarios are written and FAIL before implementation (T010→T011/T012, T014→T015,
  T017→T018, T020→T021)
- Server handler before client JS (T011 → T012)
- Rebuild + restart before any E2E run — Razor views are precompiled; a running instance
  will NOT pick up `.cshtml` changes from disk
- Story complete (green E2E) before moving to the next priority

### Foundational-Internal Dependencies

```text
T002 ─┬─> T005
      ├─> T006 <─ T003
      └─> T008
T003 ─┴─> T007
T004, T009: independent
```

### Parallel Opportunities

- **Phase 2, wave 1**: `T002 + T003 + T004 + T009` together (4 different file sets, no
  dependencies) — per Constitution Principle XI, dispatch as parallel subagent runs
- **Phase 2, wave 2**: `T005 + T006 + T007 + T008` together (different files; all deps
  satisfied by wave 1)
- **Within each story**: the E2E-scenario task `[P]` can be written while the
  implementation task proceeds (different files: spec.ts vs. cs/cshtml/css)
- **Phase 7**: T023's three test suites run in parallel with each other

---

## Parallel Example: User Story 1

```text
# After Foundational checkpoint, in the US1 phase:
Task: "Create 18-theme-preference.spec.ts with US1 scenarios (red)"        [P]
Task: "Add OnPostThemeAsync handler in Settings.cshtml.cs"                 (then)
Task: "Wire fetch-save + apply in Settings.cshtml"                         (then)
Task: "Rebuild + restart + run US1 E2E green"                              (last)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: branch
2. Phase 2: Foundational (CRITICAL — blocks all stories)
3. Phase 3: US1 → **STOP and VALIDATE**: US1 E2E green, restart evidence captured
4. Demo: sign in → Settings → Dark applies instantly → survives re-login

### Incremental Delivery

1. Setup + Foundational → theme renders from the cookie claim
2. + US1 → save/apply/persist (MVP!)
3. + US2 → Light verified paper-like (AA, no pure white)
4. + US3 → Dark verified night-reading (AA, soft dark)
5. + US4 → System live-follow, no-flash, anonymous
6. Each increment: green E2E + restart evidence before the next

### Parallel Team Strategy (Principle XI)

1. One agent/developer: Setup + wave-1 parallel subagents (T002/T003/T004/T009)
2. Wave 2 subagents (T005/T006/T007/T008) → Foundation checkpoint
3. Stories then run sequentially (shared `site.css` + spec file) — parent session keeps
   final decision authority and is the sole writer for the shared `cwd`

---

## Notes

- `[P]` tasks = different files, no dependencies on incomplete tasks
- `[Story]` label maps task to user story for traceability (US1–US4 = spec.md stories)
- Commit after each task or logical group; stop at any checkpoint to validate
- **App restart is part of the loop**: after ANY `.cshtml`/`.css` change, rebuild +
  restart before E2E (ASP.NET Core precompiles Razor views at build time)
- Anti-forgery stays implicit everywhere — never `.DisableAntiforgery()` on the new
  handler (spec 024 history)
- The claim-set pin (`AuthClaimsTests`) MUST be updated in the same change as
  `AuthClaims.Build` — it fails the build otherwise (bug-039 guard)
