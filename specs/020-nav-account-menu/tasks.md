# Tasks: Navigation Bar and Account Menu

**Input**: Design documents from `/specs/020-nav-account-menu/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: No test tasks — this is a purely presentational change (nav markup + CSS). Validation is visual via quickstart.md scenarios.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

All paths are relative to the repository root (`/workspace`).

---

## Phase 1: Setup (Branch Creation)

**Purpose**: Create the implementation branch from master.

- [ ] T001 Create branch `story/020-nav-account-menu` from `master` and check it out

---

## Phase 2: Foundational (Color Tokens and Nav Base Styles)

**Purpose**: Establish the new color palette and base nav structure that all user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until color tokens and base nav styles are in place.

- [ ] T002 [P] Add nav color custom properties to `:root` in `src/Host/wwwroot/css/site.css` (`--nav-bg: #201e1d`, `--nav-hover: #3a3634`, `--page-bg: #f5ead8`, `--accent: #c67139`, `--accent-text: #8a4f26`, `--nav-text: #c9c2ba`, `--border-light: #e4d9c8`, `--mobile-active-bg: #f3ddc9`)
- [ ] T003 [P] Update `body` background to use `--page-bg` (#f5ead8) in `src/Host/wwwroot/css/site.css`
- [ ] T004 Rewrite `.navbar` base styles in `src/Host/wwwroot/css/site.css`: background `--nav-bg`, flex row, align-items center, gap 24px, padding 12px 24px, flex-wrap wrap
- [ ] T005 Update `.navbar .brand` styles in `src/Host/wwwroot/css/site.css`: Caprasimo serif display font, 20px, color `--page-bg` (#f5ead8), margin-right 16px

**Checkpoint**: Nav bar renders with dark background and correct brand styling. Page background is #f5ead8.

---

## Phase 3: User Story 1 - Navigate Between Pages via Top Bar (Priority: P1) 🎯 MVP

**Goal**: Persistent navigation bar with brand wordmark, nav links (My Courses, Browse Courses always; Dashboard for admin view), and active link highlighting.

**Independent Test**: Open any page; verify nav bar is visible with correct colors, brand, and links. Click links to navigate; verify active link is highlighted with accent color (#c67139).

### Implementation for User Story 1

- [ ] T006 [US1] Rewrite nav links section in `src/Host/Pages/Shared/_Layout.cshtml`: replace current nav-links structure with flex-spacer (`<div style="flex:1"></div>`) pushing account control right; links for My Courses (graduation-cap icon) and Browse Courses (book-open icon) as `<a>` tags with `asp-page` routing
- [ ] T007 [US1] Add admin-conditional Dashboard link in `src/Host/Pages/Shared/_Layout.cshtml`: `<a asp-page="/Admin/Dashboard/Index">` wrapped in role-check data attribute for client-side toggle (data-role-link="admin"), icon `layout-dashboard`
- [ ] T008 [US1] Remove Organizations, Org Chart, Learners, Courses, Enrollments, Create Course, Upload SCORM links from nav (spec does not include them; these remain accessible via Dashboard page)
- [ ] T009 [US1] Style nav links in `src/Host/wwwroot/css/site.css`: 14px font, weight 600, color `--nav-text` (#c9c2ba), icon 16px stroke-width 2.75 + label, gap 6px, no border/background, padding 8px 0
- [ ] T010 [US1] Add nav link hover state in `src/Host/wwwroot/css/site.css`: color `#ffffff` on hover
- [ ] T011 [US1] Add active link detection in `src/Host/Pages/Shared/_Layout.cshtml`: compare `@Context.Request.Path` with each link target to apply `class="active"` when matching
- [ ] T012 [US1] Style active nav link in `src/Host/wwwroot/css/site.css`: color `--accent` (#c67139) for `.navbar a.active`
- [ ] T013 [US1] Update role switcher JS in `src/Host/Pages/Shared/_Layout.cshtml`: toggle admin links visibility based on `data-role` attribute; hide all `data-role-link="admin"` elements when role is "learner"

**Checkpoint**: Nav bar displays brand + links with correct colors. Active link highlighted. Admin links toggle with role switcher. User can navigate between pages.

---

## Phase 4: User Story 2 - Toggle Between Roles and Access Account Menu (Priority: P1)

**Goal**: Account control on the right side with role toggle pill (always visible), static user name, chevron icon, and click-to-open dropdown.

**Independent Test**: Click Learner/Admin buttons; verify role pill highlights correctly, Dashboard appears/disappears, user name stays constant. Click account control (not pill); verify dropdown opens/closes.

### Implementation for User Story 2

- [ ] T014 [US2] Replace avatar + profile control in `src/Host/Pages/Shared/_Layout.cshtml` with new account control: `<div class="account-control" id="account-control" role="button" tabindex="0" aria-label="Account menu" aria-expanded="false">` containing role toggle pill, name span, and chevron icon
- [ ] T015 [US2] Build role toggle pill inside account control in `src/Host/Pages/Shared/_Layout.cshtml`: `<div class="role-pill" id="role-pill">` with two `<button class="role-segment">` elements (Learner/Admin), always visible for all authenticated users (remove `@if (User.IsInRole(...))` guard)
- [ ] T016 [US2] Add static name display in `src/Host/Pages/Shared/_Layout.cshtml`: `<span class="account-name">@User.Identity.Name</span>` at 13px weight 600, color #f5ead8 — rendered outside any role-conditional block
- [ ] T017 [US2] Add chevron-down icon after name in `src/Host/Pages/Shared/_Layout.cshtml`: `<i data-lucide="chevron-down" class="account-chevron"></i>` at 14px, stroke-width 2.75, color #c9c2ba
- [ ] T018 [P] Style account control wrapper in `src/Host/wwwroot/css/site.css`: position relative, flex row, align-items center, gap 8px, padding 4px 8px 4px 4px, border-radius 999px (pill), cursor pointer, hover background `--nav-hover` (#3a3634)
- [ ] T019 [P] Style role toggle pill in `src/Host/wwwroot/css/site.css`: flex row, gap 2px, background `--nav-hover` (#3a3634), border-radius 999px, padding 3px
- [ ] T020 [P] Style role segments in `src/Host/wwwroot/css/site.css`: 12px font, weight 700, padding 6px 14px, border-radius 999px, transparent background, color #c9c2ba; active state: background `--accent` (#c67139), color #ffffff
- [ ] T021 [US2] Write account control JS handler in `src/Host/Pages/Shared/_Layout.cshtml`: click on `.account-control` toggles dropdown; click on `.role-pill` calls `event.stopPropagation()` and toggles role only
- [ ] T022 [US2] Update role switcher JS in `src/Host/Pages/Shared/_Layout.cshtml`: integrate with account control role pill; `setRole()` updates pill active state, admin link visibility, and localStorage; `stopPropagation` on pill clicks
- [ ] T023 [US2] Add keyboard support for account control in `src/Host/Pages/Shared/_Layout.cshtml`: Enter/Space toggles dropdown, Escape closes dropdown

**Checkpoint**: Account control shows role pill + name + chevron. Role toggle changes nav links without changing name. Click opens/closes dropdown.

---

## Phase 5: User Story 3 - Access Profile and Settings from Account Dropdown (Priority: P2)

**Goal**: Dropdown menu with View Profile and Settings rows (with icons), no Logout.

**Independent Test**: Open account dropdown; verify two rows visible. Click View Profile → navigates to /Account/Profile. Click Settings → navigates to /Account/Settings. No Logout in dropdown.

### Implementation for User Story 3

- [ ] T024 [US3] Build dropdown markup in `src/Host/Pages/Shared/_Layout.cshtml`: `<div class="account-dropdown" id="account-dropdown">` inside account control, containing two `<a>` rows: View Profile (user icon, `asp-page="/Account/Profile"`) and Settings (settings icon, `asp-page="/Account/Settings"`), each with icon 15px stroke-width 2.75, gap 10px
- [ ] T025 [US3] Style dropdown in `src/Host/wwwroot/css/site.css`: position absolute, top `calc(100% + 8px)`, right 0, min-width 190px, padding 8px, white background, border-radius 12px, subtle shadow, z-index 1000, display none by default, `.is-open` shows display block
- [ ] T026 [US3] Style dropdown rows in `src/Host/wwwroot/css/site.css`: full width, flex row, align-items center, gap 10px, padding 8px 12px, border-radius 8px, 14px font, text-align left, no border/background; hover: background `--border-light` (#e4d9c8)
- [ ] T027 [US3] Add dropdown close-on-outside-click handler in `src/Host/Pages/Shared/_Layout.cshtml`: `document.addEventListener('click', ...)` checks if click target is outside `.account-control`, closes dropdown
- [ ] T028 [US3] Add chevron rotation in `src/Host/wwwroot/css/site.css`: `.account-control.is-open .account-chevron` gets `transform: rotate(180deg)` with transition

**Checkpoint**: Dropdown opens with View Profile and Settings. Clicking navigates correctly. Closes on outside click. No Logout present.

---

## Phase 6: User Story 4 - Logout from Settings Page (Priority: P2)

**Goal**: Verify Logout exists on Settings page as last row. Confirm no Logout in top nav.

**Independent Test**: Navigate to Settings; verify Logout is the last row after Email notifications and Theme. Check top nav — no Logout visible.

### Implementation for User Story 4

- [ ] T029 [US4] Verify `src/Host/Pages/Account/Settings.cshtml` has Logout as last row of preferences list (after Email notifications and Theme) — no edit expected, confirm structure matches spec

**Checkpoint**: Settings page has Logout as last row. Top nav has zero Logout instances.

---

## Phase 7: User Story 5 - Mobile Navigation Experience (Priority: P3)

**Goal**: At ≤760px, hamburger button replaces nav links; hamburger dropdown contains role toggle pill + page links; account name hides; page layout adjusts.

**Independent Test**: Resize to 760px; verify hamburger visible, nav links hidden. Click hamburger; verify dropdown with role pill + links. Verify account name hidden. Verify page heading 28px, reduced padding, stacked layouts.

### Implementation for User Story 5

- [ ] T030 [US5] Add hamburger button to `src/Host/Pages/Shared/_Layout.cshtml`: `<button class="hamburger-toggle" id="nav-toggle" aria-label="Toggle navigation" aria-expanded="false">` with menu icon (20px, stroke-width 2.75, padding 6px), visible only at ≤760px
- [ ] T031 [US5] Restructure mobile nav in `src/Host/Pages/Shared/_Layout.cshtml`: at ≤760px, nav links hidden by default; hamburger click opens a dropdown (absolute, top `calc(100% + 8px)`, left/right 16px, card surface) containing role toggle pill (centered, margin 8px) then page links stacked as full-width rows
- [ ] T032 [US5] Hide account name on mobile in `src/Host/wwwroot/css/site.css`: `.account-name { display: none; }` inside `@media (max-width: 760px)` block
- [ ] T033 [P] Style mobile hamburger dropdown in `src/Host/wwwroot/css/site.css`: absolute position, card surface (white bg, border-radius 12px, shadow), grid gap 2px, padding 8px; page links as full-width rows with padding 12px, border-radius 8px
- [ ] T034 [P] Style mobile active link in `src/Host/wwwroot/css/site.css`: background `--mobile-active-bg` (#f3ddc9), text color `--accent-text` (#8a4f26) for active links in hamburger menu
- [ ] T035 [P] Style hamburger button in `src/Host/wwwroot/css/site.css`: 20px icon, stroke-width 2.75, padding 6px, hover background `--nav-hover` (#3a3634), hidden at >760px
- [ ] T036 [US5] Write hamburger toggle JS in `src/Host/Pages/Shared/_Layout.cshtml`: click toggles `.is-open` on nav-links dropdown; update aria-expanded; close on outside click
- [ ] T037 [P] Add page-level mobile adjustments in `src/Host/wwwroot/css/site.css` (`@media (max-width: 760px)`): `h1 { font-size: 28px; }`, `.container { padding: 24px 16px 32px; }`, `.filters { flex-direction: column; }`, `.flex-row, .flex-between { flex-direction: column; }`
- [ ] T038 [US5] Hide desktop nav links at ≤760px and show hamburger dropdown: update existing `@media (max-width: 760px)` block in `src/Host/wwwroot/css/site.css` to use the new mobile dropdown pattern

**Checkpoint**: At ≤760px, hamburger replaces nav links. Dropdown shows role pill + page links. Account name hidden. Page heading 28px, reduced padding, stacked layouts.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Clean up legacy styles, ensure no regressions, validate complete nav behavior.

- [ ] T039 Remove old `.nav-profile`, `.nav-role-switcher` (standalone), and avatar CSS from `src/Host/wwwroot/css/site.css` that are superseded by the new account control styles
- [ ] T040 Remove old desktop nav media query overrides (`@media (min-width: 761px)`) from `src/Host/wwwroot/css/site.css` that reference the previous nav structure
- [ ] T041 [P] Audit `src/Host/wwwroot/css/site.css` for any remaining references to `--color-brand` in nav selectors; replace with `--nav-bg` or remove
- [ ] T042 Run `dotnet test tests/ArchitectureTests` to confirm no module boundary violations
- [ ] T043 Run quickstart.md validation scenarios at 375px, 760px, 761px, 1280px, 1920px viewports
- [ ] T044 Verify all interactive elements have hover states: nav links (#ffffff), account control (#3a3634), dropdown rows (#e4d9c8), hamburger (#3a3634)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — create branch immediately
- **Foundational (Phase 2)**: Depends on branch creation — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational — nav links and active detection
- **US2 (Phase 4)**: Depends on Foundational — account control with role pill (modifies same files as US1 but different DOM regions)
- **US3 (Phase 5)**: Depends on US2 — dropdown lives inside account control
- **US4 (Phase 6)**: No dependencies — verification only
- **US5 (Phase 7)**: Depends on US1 + US2 — mobile uses the same nav elements with CSS media queries
- **Polish (Phase 8)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — independent of other stories
- **US2 (P1)**: Can start after Foundational — independent but touches same files as US1
- **US3 (P2)**: Depends on US2 (dropdown inside account control)
- **US4 (P2)**: Independent — verification only
- **US5 (P3)**: Depends on US1 + US2 (mobile adapts the desktop nav)

### Parallel Opportunities

- T002 and T003: [P] — color tokens and body background are independent CSS edits
- T018, T019, T020: [P] — account control, role pill, and role segment styles are separate CSS blocks
- T033, T034, T035, T037: [P] — mobile styles are separate media query blocks
- T039 and T041: [P] — CSS cleanup tasks target different sections
- US4 (T029): Can run in parallel with any phase — it's a verification-only task

---

## Parallel Example: Foundational Phase

```bash
# Launch color token tasks in parallel:
Task: "Add nav color custom properties to :root in site.css"
Task: "Update body background to use --page-bg in site.css"

# Then sequentially:
Task: "Rewrite .navbar base styles in site.css"
Task: "Update .navbar .brand styles in site.css"
```

## Parallel Example: User Story 2

```bash
# Launch CSS style tasks in parallel:
Task: "Style account control wrapper in site.css"
Task: "Style role toggle pill in site.css"
Task: "Style role segments in site.css"

# Then sequentially:
Task: "Build role toggle pill markup in _Layout.cshtml"
Task: "Add static name display in _Layout.cshtml"
Task: "Write account control JS handler in _Layout.cshtml"
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1: Branch creation
2. Complete Phase 2: Foundational (color tokens + nav base)
3. Complete Phase 3: Nav links with active detection (US1)
4. Complete Phase 4: Account control with role toggle (US2)
5. **STOP and VALIDATE**: Nav bar displays correctly, role toggle works, links navigate
6. This delivers the core nav experience

### Incremental Delivery

1. Setup + Foundational → Nav renders with new colors
2. US1 → Links work with active highlighting
3. US2 → Role toggle and account control work
4. US3 → Dropdown with Profile/Settings
5. US4 → Verify Settings logout (no changes)
6. US5 → Mobile responsive nav
7. Polish → Cleanup and validation

---

## Notes

- All tasks modify only `src/Host/Pages/Shared/_Layout.cshtml` and `src/Host/wwwroot/css/site.css` (plus T029 verification of Settings.cshtml)
- No server-side code changes, no module changes, no database changes
- ArchitectureTests must pass — no module boundary violations
- Commit after each phase for clean incremental history
