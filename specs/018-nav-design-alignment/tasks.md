# Tasks: Nav & Header Design Alignment

**Input**: Design documents from `/specs/018-nav-design-alignment/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md

**Organization**: Tasks grouped by user story. Two files affected: `_Layout.cshtml` and `site.css`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files or non-overlapping regions)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup

**Purpose**: Branch creation and foundation

- [x] T001 Create branch `story/018-nav-design-alignment` from `master`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add Lucide icon library — required before any icon replacement

**⚠️ CRITICAL**: No icon replacement tasks can begin until Lucide is loaded

- [x] T002 [P] [US1] Add Lucide CDN `<script>` tag to `<head>` in `src/Host/Pages/Shared/_Layout.cshtml` and add `lucide.createIcons({ attributes: { 'stroke-width': 2.75 } })` initialization
- [x] T003 [P] [US1] Add Lucide icon base CSS rules in `src/Host/wwwroot/css/site.css` — `.lucide-icon { display: inline-block; vertical-align: middle; margin-right: var(--spacing-sm); }` and sizing rules

**Checkpoint**: Lucide loaded, icons render at stroke-width 2.75

---

## Phase 3: User Story 1 — Desktop Nav (Priority: P1) 🎯 MVP

**Goal**: Replace emoji icons with Lucide SVGs, add role switcher, fix profile dropdown, remove nav Logout

**Independent Test**: Login as learner, verify SVG icons, role switcher toggles, profile dropdown shows View Profile + Settings, no Logout in nav

### Implementation for User Story 1

- [x] T004 [US1] Replace all emoji HTML entities with `<i data-lucide="..."></i>` tags in `src/Host/Pages/Shared/_Layout.cshtml` nav links (Browse Courses→book-open, My Courses→graduation-cap, Dashboard→layout-dashboard, Organizations→building-2, Org Chart→network, Learners→users, Courses→book, Enrollments→clipboard-list, Create Course→plus-circle, Upload SCORM→upload, Login→log-in)
- [x] T005 [US1] Add role switcher (pill-shaped segmented control) markup to `src/Host/Pages/Shared/_Layout.cshtml` — positioned between `.nav-links` and `.nav-profile`, with "Learner" and "Admin" segments
- [x] T006 [P] [US1] Add role switcher CSS styles to `src/Host/wwwroot/css/site.css` — pill-shaped container, active/inactive segment styles using Organic tokens (var(--color-surface), var(--color-brand), var(--radius-pill)), admin-link show/hide via `.nav-role-switcher[data-role="admin"] ~ .nav-links .admin-link { display: none; }` pattern
- [x] T007 [US1] Add role switcher JavaScript to `src/Host/Pages/Shared/_Layout.cshtml` — segment click handler, `localStorage.setItem('nav-role-view', ...)` persistence, admin link toggle on mount (read from localStorage or default "learner")
- [x] T008 [US1] Remove the standalone "Logout" link (`<div class="nav-user">...Logout...</div>`) from `src/Host/Pages/Shared/_Layout.cshtml` nav-links block
- [x] T009 [US1] Replace profile dropdown arrow (`&#9660;`) with Lucide `chevron-down` icon in `src/Host/Pages/Shared/_Layout.cshtml`

**Checkpoint**: Desktop nav shows SVG icons, role switcher toggles links, profile dropdown works, no Logout in nav

---

## Phase 4: User Story 2 — Mobile Nav ≤760px (Priority: P2)

**Goal**: Collapse nav behind hamburger at 760px, show role switcher in mobile menu, hide name label

**Independent Test**: Resize to 375px, verify brand+hamburger+avatar visible, hamburger opens dropdown with role switcher + links

### Implementation for User Story 2

- [x] T010 [US2] Replace hamburger Unicode icons (`&#9776;`/`&#10006;`) with Lucide `menu`/`x` icons in `src/Host/Pages/Shared/_Layout.cshtml` — use `data-lucide` attributes with JS-based toggle (swap icon name on open/close)
- [x] T011 [P] [US2] Update mobile breakpoint in `src/Host/wwwroot/css/site.css` — change nav-specific `@media (max-width: 480px)` rules to `@media (max-width: 760px)` for the navbar, hamburger visibility, and nav-links collapse. Keep page-level styles at 480px breakpoint. Add `@media (min-width: 761px)` block to restore desktop nav layout.
- [x] T012 [US2] Move role switcher inside `.nav-links` in `src/Host/Pages/Shared/_Layout.cshtml` (inside the hamburger-toggleable region) so it appears in the mobile dropdown; position it before the page links. On desktop (≥761px), use CSS to position it between links and profile via absolute/flex ordering.
- [x] T013 [US2] Verify `.nav-profile .profile-name { display: none; }` is set in the mobile breakpoint CSS (already exists at 480px — add to 760px block or ensure the mobile rule applies at 760px)

**Checkpoint**: Mobile nav at 375px shows brand+hamburger+avatar; hamburger opens dropdown with role switcher + links

---

## Phase 5: User Story 3 — CSS Token Audit (Priority: P3)

**Goal**: Ensure all nav CSS uses design tokens — no hardcoded hex or px values

**Independent Test**: Grep nav CSS rules for raw `#` hex and raw `px` values — both must return zero matches outside `:root`

### Implementation for User Story 3

- [x] T014 [US3] Audit `src/Host/wwwroot/css/site.css` nav rules for hardcoded values — replace any raw hex colors with `var(--color-...)` tokens and any raw px values with `var(--spacing-...)`, `var(--radius-...)`, or `var(--font-size-...)` tokens. Flag: `rgba(255,255,255,0.15)`, `rgba(0,0,0,0.1)`, `48px`, `26px`, `20px` in toggle switch, `32px` on avatar.

**Checkpoint**: `grep -n '#[0-9a-f]' site.css` and `grep -n '[0-9]px' site.css` return zero nav-related matches

---

## Phase 6: Polish & Validation

**Purpose**: Final verification

- [x] T015 Validate all quickstart.md scenarios pass (desktop, mobile, token audit, unauthenticated)
- [x] T016 [P] Verify no emoji characters remain anywhere in `_Layout.cshtml` (`grep '&#128\|&#97\|&#127\|&#101\|&#999' _Layout.cshtml` returns nothing)
- [x] T017 Commit changes and push branch `story/018-nav-design-alignment`
- [x] T018 Merge to `master` and push
- [x] T019 Switch back to `master` (Constitution XII)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on branch creation — blocks all icon work
- **US1 (Phase 3)**: Depends on Lucide being loaded (T002) — T006 can run in parallel with T005/T007
- **US2 (Phase 4)**: Depends on US1 complete (role switcher must exist before mobile integration)
- **US3 (Phase 5)**: Can start after T006 (any new CSS added must be audited)
- **Polish (Phase 6)**: All stories complete

### Parallel Opportunities

- T002 + T003 can run in parallel (different files: `_Layout.cshtml` head + `site.css`)
- T006 (CSS) can run in parallel with T005 (markup) — different files
- T010 (hamburger SVG) + T011 (breakpoint CSS) can run in parallel — different files

---

## Notes

- Both `_Layout.cshtml` and `site.css` are single files with significant edits — coordinate T004/T005/T007/T008/T009 to avoid conflicting edits on the same regions
- Role switcher must be inside `.nav-links` for mobile hamburger integration (T012) — plan T005 markup placement accordingly
- Mobile breakpoint change is nav-only (760px for nav, 480px for pages) — be precise with media query selectors
