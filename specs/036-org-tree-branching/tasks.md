# Tasks: Organization Tree Branching in Admin Organizations

**Input**: Design documents from `/specs/036-org-tree-branching/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/organization-tree-ui.md ✅, quickstart.md ✅

**Tests**: Included — Constitution Principle XIII mandates E2E validation for any feature ("A fix that compiles but has no E2E test is unverified"). E2E tasks extend the existing `tests/Playwright.Tests/tests/06-admin-organizations.spec.ts`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies) — dispatched as parallel subagent runs per Constitution Principle XI
- **[Story]**: Which user story this task belongs to (US1–US4)
- Contract rule references (C-xx, B-xx) point to `contracts/organization-tree-ui.md`; M-x to `quickstart.md` §5.

---

## Phase 1: Setup

**Purpose**: Branch discipline before any code change (Constitution VIII)

- [X] T001 Create branch `story/036-org-tree-branching` from `master` (`git checkout master && git checkout -b story/036-org-tree-branching`); verify clean working tree

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared data + failing tests that every story builds on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Extend `OrgTreeNode` record with `bool IsDisabled` in `src/Host/Pages/Admin/Organizations/Index.cshtml.cs` and compute it in `BuildTree` as *own flag OR any ancestor's flag* (data-model.md; no service/module changes)
- [X] T003 [P] [US1][US2] Add failing E2E assertions to `tests/Playwright.Tests/tests/06-admin-organizations.spec.ts`: single top-level node with `Root` badge (C-01, C-07); every org renders exactly once (C-01); Billing's `<li>` is a DOM descendant of Finance's and not of Sales' (C-02/C-03); Finance and Sales share one parent `<ul>` (C-04). **Must FAIL before Phase 3 markup exists** (verify the failure, then proceed)

**Checkpoint**: Foundation ready — view model carries disabled state; US1/US2 tests written and failing

---

## Phase 3: User Story 1 - See the Organization Hierarchy at a Glance (Priority: P1) 🎯 MVP

**Goal**: The Organizations page renders a true hierarchical tree — depth-based indentation, CSS parent→child connector lines, visually distinguished root (spec FR-001…FR-005, FR-006, FR-008)

**Independent Test**: Seed Root → Finance, Sales; Finance → Billing (quickstart §3); open the page: Billing one level deeper than Finance/Sales, connector lines trace to the correct parent, root row visually distinct, no dangling lines under leaves

### Implementation for User Story 1

- [ ] T004 [P] Rewrite `src/Host/Pages/Shared/_OrgNode.cshtml` as semantic nested-list markup: `<li class="org-node">` per org, children in a nested `<ul class="org-tree org-tree--nested">`, card with name/description (only when present)/Edit action; `org-node--root` + `Root` badge on the root; **remove all inline `margin-left` styles**; HTML-encoded names (C-01…C-05, C-07, C-09, C-12)
- [ ] T005 [P] Add tree styles to `src/Host/wwwroot/css/site.css` using existing Organic tokens only: `--org-tree-indent` variable, CSS elbow connectors (pseudo-element vertical + horizontal lines, `--color-border`/`--color-border-strong`) on non-root `<li>`s, stronger root-card styling (brand-tinted border/background) (C-06, C-07, C-10; R1–R3, R5)
- [ ] T006 Update `src/Host/Pages/Admin/Organizations/Index.cshtml`: wrap the root loop in `<ul class="org-tree" aria-label="Organization hierarchy">`; keep Org Chart View / Create Organization entry points and error alert intact (C-01, FR-011; B-01)
- [ ] T007 Rebuild + restart (`./scripts/restart-app.sh --background` from repo root; confirm `Build succeeded` + `Now listening` per quickstart §2) and manually verify M1–M4 against the seeded hierarchy
- [ ] T008 Run `npx playwright test tests/06-admin-organizations.spec.ts` (from `tests/Playwright.Tests`) — T003's US1 assertions now PASS; existing tests still pass

**Checkpoint**: MVP — hierarchy visible at a glance; US1 independently testable and verified

---

## Phase 4: User Story 2 - Trace Parent and Sibling Relationships (Priority: P2)

**Goal**: Parent/child and sibling relationships are traceable from the tree alone (spec FR-003, FR-004; SC-001, SC-002)

**Independent Test**: From the tree alone, correctly state: Billing's parent is Finance; Finance and Sales are siblings under root; Billing is unrelated to Sales — in under 5 seconds

### Implementation for User Story 2

- [ ] T009 Verify US2 acceptance via the Phase 3 E2E traceability assertions (T003) plus the SC-001/SC-002 identification trial (quickstart §5 M2/M3). Structurally delivered by US1 markup — **no new code expected**; if any connector is ambiguous at 3 levels, fix line geometry in `src/Host/wwwroot/css/site.css` and re-run T008

**Checkpoint**: US1 + US2 both verified independently

---

## Phase 5: User Story 3 - Manage Organizations From the Tree (Priority: P3)

**Goal**: No loss of existing management actions; disabled orgs and their descendants render visibly distinct (spec FR-006, FR-007, FR-011)

**Independent Test**: Edit on any row opens the existing edit screen unchanged; Create Organization still works and the new node lands in the correct tree position; a disabled org's whole subtree renders muted with `Disabled` badges while staying visible

### Implementation for User Story 3

- [ ] T010 [P] [US3] Add disabled treatment to `src/Host/Pages/Shared/_OrgNode.cshtml`: `org-node--disabled` class + `Disabled` badge when `Model.IsDisabled` (C-08; R4)
- [ ] T011 [P] [US3] Add E2E assertions to `tests/Playwright.Tests/tests/06-admin-organizations.spec.ts`: a disabled node and all descendants carry `org-node--disabled` + `Disabled` badge and remain visible in place (C-08); create-organization flow still lands the new node in the correct nesting (B-03)
- [ ] T012 [P] [US3] Add disabled-node styles to `src/Host/wwwroot/css/site.css`: muted treatment using existing tokens (reduced opacity / `--color-text-faint`) for `org-node--disabled`; no strike-through (C-08, C-10; R4)
- [ ] T013 [US3] Verify US3 acceptance: seed a disabled org via the Org Chart context menu (quickstart §3 step 4), confirm subtree treatment (M5/M6 + C-08); confirm Edit/Create/Chart entry points unchanged (FR-011)

**Checkpoint**: US1–US3 all independently functional

---

## Phase 6: User Story 4 - Small Screens and Deep Hierarchies (Priority: P4)

**Goal**: Tree stays usable at 375px and at 6+ levels of depth, with no horizontal scrolling (spec FR-009, FR-010; SC-004)

**Independent Test**: Load the page at a 375px viewport (and an optional 7-level hierarchy): no horizontal scroll, all node names readable, connectors intact

### Implementation for User Story 4

- [ ] T014 [P] Add media queries to `src/Host/wwwroot/css/site.css` reducing `--org-tree-indent` at the existing breakpoints (≤480px base, ≤760px) so deep trees fit ≥375px viewports (C-11; R5)
- [ ] T015 [P] [US4] Add E2E to `tests/Playwright.Tests/tests/06-admin-organizations.spec.ts`: at 375px viewport the tree container satisfies `scrollWidth <= clientWidth` (C-11, SC-004)
- [ ] T016 Rebuild + restart; verify M8 (375px) and M9 (optional: seed 7 levels, deepest node still traceable) per quickstart §5

**Checkpoint**: All four user stories independently functional and verified

---

## Phase 7: Polish & Cross-Cutting Concerns (Verification Gates)

**Purpose**: Constitution Principle XIII — no completion claims without evidence from all three gates

- [ ] T017 Gate 2 (E2E): full Playwright regression — `npx playwright test` (all specs, from `tests/Playwright.Tests`) passes; show output
- [ ] T018 Gate 2 (architecture): `dotnet test tests/ArchitectureTests` passes (Principle III)
- [ ] T019 Gate 3 (post-merge): fast-forward merge `story/036-org-tree-branching` into `master`, rebuild + restart on merged code, re-run `npx playwright test tests/06-admin-organizations.spec.ts` — passes; show output
- [ ] T020 Bookkeeping on `master`: set spec.md `Status` to `Complete (merged <date>)` and commit `docs(036): mark complete` directly on master (project convention); confirm session ends on `master` (Principle XII)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — T001 first, always
- **Foundational (Phase 2)**: Depends on T001 — BLOCKS all user stories
- **User Stories (Phases 3–6)**: Sequential in priority order (P1 → P2 → P3 → P4) because US1 and US2 share `_OrgNode.cshtml`/`site.css` (no same-file parallelism across stories), and later stories extend markup/styles delivered earlier
- **Polish (Phase 7)**: Depends on all user stories; T019 depends on T017+T018; T020 depends on T019

### User Story Dependencies

- **US1 (P1)**: Needs T002 (view model) + T003 (failing tests)
- **US2 (P2)**: Structurally delivered by US1; verification-only phase (T009)
- **US3 (P3)**: Needs T002 (`IsDisabled` propagation); extends US1 markup/styles
- **US4 (P4)**: Extends US1 CSS (indent variable introduced in T005); independent of US3

### Within Each User Story

- Tests written and confirmed failing before implementation (T003 before T004/T005; T011 before/parallel with T010/T012)
- Markup (T004/T010) and CSS (T005/T012/T014) in different files — parallel within a story
- Rebuild + restart (T007/T016) before any visual or E2E verification — Razor views are precompiled, no hot reload

### Parallel Opportunities (Principle XI — dispatch [P] groups as parallel subagent runs)

- Phase 2: `T002 ∥ T003` (Index.cshtml.cs ∥ spec.ts)
- US1: `T004 ∥ T005` (_OrgNode.cshtml ∥ site.css)
- US3: `T010 ∥ T011 ∥ T012` (_OrgNode.cshtml ∥ spec.ts ∥ site.css)
- US4: `T014 ∥ T015` (site.css ∥ spec.ts)

```bash
# Example parallel dispatch (US3):
Task: "T010 add disabled class + badge to _OrgNode.cshtml"
Task: "T011 add disabled-subtree E2E assertions to 06-admin-organizations.spec.ts"
Task: "T012 add org-node--disabled muted styles to site.css"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001 → Phase 2 (T002 ∥ T003, confirm T003 fails) → Phase 3
2. **STOP and VALIDATE** at the Phase 3 checkpoint: hierarchy visible, root distinct, lines traceable, US1 E2E green — this alone fixes the reported defect

### Incremental Delivery

1. US1 → validate (MVP: the reported bug is fixed)
2. US2 → validate (traceability confirmed, SC-001/SC-002)
3. US3 → validate (disabled treatment + no action regression)
4. US4 → validate (mobile + depth)
5. Phase 7 → merge with full gate evidence

### Single-Writer Note

All tasks run in the shared `cwd` on the feature branch; [P] groups are safe only because they touch disjoint files. The parent session owns the merge (T019) — never delegate it to a child.

---

## Notes

- [P] tasks = different files, no dependencies; dispatch via pi subagents (Principle XI)
- Every contract rule C-01…C-12 has a task + an E2E or manual check; every FR-001…FR-011 is covered (FR-008 by T005/T012/T014 token discipline + M7 visual review)
- Restart is mandatory after any `.cshtml`/CSS edit (precompiled Razor views) — see `restart-host-app` skill pitfalls (explicit `pkill -f 'bin/Debug/net10.0/Host'`, port check)
- Test data is UI-seeded (persistent dev DB) per quickstart §3 — do not assume seeders recreate the standard hierarchy
- Commit after each task or logical group; stop at any checkpoint to validate independently
