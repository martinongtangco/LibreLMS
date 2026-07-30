# Tasks: Clean Up Orphaned HTMX Handler and Update Spec 005 Artifacts

**Input**: Design documents from `/specs/006-cleanup-htmx-dead-code/`

**Prerequisites**: plan.md (tech stack, structure), spec.md (3 user stories), research.md (3 decisions), data-model.md (handler removal), quickstart.md (6 validation scenarios)

**Tests**: Not explicitly requested in the feature specification. Test tasks are excluded.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No setup needed — this is a cleanup slice against an existing working codebase. All projects, packages, and tooling already exist.

*(No setup tasks — proceed directly to user story phases.)*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No blocking prerequisites — each user story can proceed independently.

*(No foundational tasks — proceed directly to user story phases.)*

---

## Phase 3: User Story 1 - Remove Orphaned HTMX Handler Code (Priority: P1) 🎯 MVP

**Goal**: Remove the `OnGetDetailAsync` handler from `Detail.cshtml.cs` after confirming no other source file references it.

**Independent Test**: `grep -rn "OnGetDetailAsync" src/Host/ --include="*.cs" --include="*.cshtml"` returns zero hits. Application builds successfully.

### Implementation for User Story 1

- [X] T001 [US1] Search all source files for references to `OnGetDetailAsync` in `src/Host/` to confirm it is only defined in `src/Host/Pages/Courses/Detail.cshtml.cs` and not called anywhere else
- [X] T002 [US1] Remove the `OnGetDetailAsync` method and its XML comment from `src/Host/Pages/Courses/Detail.cshtml.cs`
- [X] T003 [US1] Run `dotnet build LearningLms.slnx` to verify the removal causes no compilation errors

**Checkpoint**: At this point, `OnGetDetailAsync` is fully removed, no source references remain, and the application builds cleanly.

---

## Phase 4: User Story 2 - Update Spec 005 tasks.md (Priority: P2)

**Goal**: Update task descriptions in `specs/005-fix-view-details-navigation/tasks.md` to accurately reflect the actual implementation (HTMX removed, not modified) and annotate superseded tasks.

**Independent Test**: Read T004/T005 — descriptions match actual code. Read T008-T010 — annotated as superseded.

### Implementation for User Story 2

- [X] T004 [P] [US2] Update T004 description in `specs/005-fix-view-details-navigation/tasks.md`: change from "Fix `hx-push-url` on View Details button" to reflect that HTMX attributes were **removed** (not changed) from the "View Details" button in `src/Host/Pages/Shared/_CourseCard.cshtml`, replaced with `asp-page` tag helper for full-page navigation
- [X] T005 [P] [US2] Update T005 description in `specs/005-fix-view-details-navigation/tasks.md`: apply the same correction for the course title link — HTMX attributes were **removed**, not modified
- [X] T006 [US2] Add a superseded annotation to T008-T010 in `specs/005-fix-view-details-navigation/tasks.md`: note that these verification tasks are superseded because HTMX inline swap from the card was abandoned in favor of full-page navigation; the `OnGetDetailAsync` handler they reference no longer has callers

**Checkpoint**: tasks.md T004/T005 accurately describe the implementation done. T008-T010 are clearly marked as superseded.

---

## Phase 5: User Story 3 - Update Spec 005 spec.md (Priority: P2)

**Goal**: Annotate US4 and FR-006 in `specs/005-fix-view-details-navigation/spec.md` to record the decision to use full-page navigation instead of HTMX inline swap.

**Independent Test**: Read US4 — annotated as abandoned with rationale. Read FR-006 — notes full-page navigation is the primary approach.

### Implementation for User Story 3

- [X] T007 [P] [US3] Add a superseded annotation to User Story 4 in `specs/005-fix-view-details-navigation/spec.md`: note that HTMX inline swap was intentionally abandoned in favor of full-page navigation via `asp-page` tag helpers; rationale: simpler, more reliable, works without JavaScript, eliminates HTMX/full-page conflict
- [X] T008 [P] [US3] Update FR-006 in `specs/005-fix-view-details-navigation/spec.md`: change from "graceful degradation when HTMX unavailable" to reflect that full-page navigation IS the primary approach (no HTMX on course cards); HTMX remains only for catalog filtering
- [X] T009 [US3] Update the Edge Cases section in `specs/005-fix-view-details-navigation/spec.md`: remove or annotate the "HTMX inline swap" edge case as no longer applicable; add a note about spec 004 (`004-htmx-razor-conversion`) potentially having cross-spec inconsistency

**Checkpoint**: spec.md US4 is annotated as abandoned, FR-006 is updated, edge cases reflect current state.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate all changes against the quickstart scenarios and ensure no regressions.

- [X] T010 Run all 6 validation scenarios from `specs/006-cleanup-htmx-dead-code/quickstart.md` and confirm pass
- [X] T011 [P] Run `dotnet build LearningLms.slnx` one final time to confirm clean build after all changes
- [X] T012 [P] Verify course catalog navigation works end-to-end: browse catalog → click "View Details" → detail page renders → browser refresh → detail page re-renders → browser back → catalog page

---

## Dependencies & Execution Order

### Phase Dependencies

- **User Story 1 (Phase 3)**: No dependencies — start immediately (code removal)
- **User Story 2 (Phase 4)**: Independent of US1 — documentation update only, can run in parallel
- **User Story 3 (Phase 5)**: Independent of US1/US2 — documentation update only, can run in parallel
- **Polish (Phase 6)**: Depends on all user stories — validation after all changes

### User Story Dependencies

- **US1 (P1)**: Core fix — remove dead code. MVP on its own.
- **US2 (P2)**: Documentation accuracy — no code changes, independent of US1
- **US3 (P2)**: Documentation accuracy — no code changes, independent of US1/US2

### Parallel Opportunities

- T004 and T005 can run in parallel (adjacent edits in same file, non-overlapping)
- T007 and T008 can run in parallel (different sections of spec.md)
- US1, US2, US3 can all proceed in parallel (code change in one file, doc changes in another)
- T011 and T012 can run in parallel (build check and browser test)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 3: Remove `OnGetDetailAsync` handler (T001-T003)
2. **STOP and VALIDATE**: Build succeeds, no source references remain
3. If all pass, the core cleanup is done

### Incremental Delivery

1. T001-T003 → US1 done (dead code removed)
2. T004-T006 → US2 done (tasks.md updated)
3. T007-T009 → US3 done (spec.md updated)
4. T010-T012 → Polish (validation, build, end-to-end test)

### Key Change Summary

**Source code** (1 file):
```
src/Host/Pages/Courses/Detail.cshtml.cs
  - REMOVE: OnGetDetailAsync method (lines ~59-70)
```

**Documentation** (2 files):
```
specs/005-fix-view-details-navigation/tasks.md
  - UPDATE: T004/T005 descriptions (HTMX removed, not modified)
  - ANNOTATE: T008-T010 as superseded

specs/005-fix-view-details-navigation/spec.md
  - ANNOTATE: US4 as intentionally abandoned
  - UPDATE: FR-006 (full-page nav is primary, not fallback)
  - UPDATE: Edge cases (remove HTMX inline swap case)
```

---

## Notes

- This is a minimal-change cleanup: 1 method removed from source, 2 documentation files annotated
- No behavioral changes to the running application
- All validation is manual (grep, build, browser navigation)
- Spec 004 cross-spec inconsistency is noted but not resolved (out of scope)
