# Feature Specification: Clean Up Orphaned HTMX Handler and Update Spec 005 Artifacts

**Feature Branch**: `bug/006-cleanup-htmx-dead-code`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features.

**Created**: 2025-07-30

**Status**: Draft

**Input**: User description: "Clean up orphaned `OnGetDetailAsync` HTMX handler, update tasks.md to reflect actual implementation approach, and update spec to record the decision to abandon HTMX inline swap in favor of full-page navigation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Remove Orphaned HTMX Handler Code (Priority: P1)

A developer reviewing the codebase should not find the `OnGetDetailAsync` handler in `Detail.cshtml.cs` — it was an HTMX endpoint for inline course-detail swapping that is no longer called by any view after the fix in spec 005 removed HTMX attributes from `_CourseCard.cshtml`. Leaving it creates confusion about intended behavior and adds dead code to maintain.

**Why this priority**: Dead code that looks functional misleads future developers (and AI agents) into thinking HTMX inline swap from the catalog is supported. Removing it prevents incorrect assumptions and reduces technical debt.

**Independent Test**: Search the codebase for `OnGetDetailAsync` — confirm it no longer exists in `Detail.cshtml.cs`. Confirm no other file references it.

**Acceptance Scenarios**:

1. **Given** the `OnGetDetailAsync` method is removed from `Detail.cshtml.cs`, **When** a developer searches the codebase for the method name, **Then** no hits appear in the Host project
2. **Given** the handler is removed, **When** the application runs and a user clicks "View Details" or the course title, **Then** full-page navigation still works correctly via `asp-page` tag helpers
3. **Given** the handler is removed, **When** the application builds, **Then** no compilation errors occur

---

### User Story 2 - Update Spec 005 tasks.md to Match Implementation (Priority: P2)

A developer reading `specs/005-fix-view-details-navigation/tasks.md` should see task descriptions that accurately reflect what was actually implemented, not the originally planned approach. The tasks were marked complete but T004/T005 describe changing `hx-push-url` when the actual fix removed HTMX from the card entirely.

**Why this priority**: Inaccurate task records mislead anyone reviewing what was done. The tasks.md is the execution record — it should match reality so future work can build on accurate information.

**Independent Test**: Read T004 and T005 in tasks.md — verify descriptions match the actual code in `_CourseCard.cshtml`.

**Acceptance Scenarios**:

1. **Given** tasks.md is updated, **When** a developer reads T004, **Then** the description reflects that HTMX attributes were removed (not changed) from the "View Details" button
2. **Given** tasks.md is updated, **When** a developer reads T005, **Then** the description reflects that HTMX attributes were removed from the course title link
3. **Given** tasks.md is updated, **When** a developer reads T008-T010 (US4 tasks), **Then** they are annotated as superseded by the full-page navigation approach

---

### User Story 3 - Update Spec 005 spec.md to Record US4 Decision (Priority: P2)

A developer reading `specs/005-fix-view-details-navigation/spec.md` should understand that US4 (HTMX inline swap) was intentionally abandoned in favor of full-page navigation, not deferred or forgotten. The spec should document this architectural decision and its rationale.

**Why this priority**: Without this record, the gap between spec (US4 defined) and implementation (US4 not implemented) appears as unfinished work rather than a deliberate trade-off. Future developers may attempt to re-implement something that was consciously simplified away.

**Independent Test**: Read spec.md — verify US4 is annotated with the decision and rationale.

**Acceptance Scenarios**:

1. **Given** spec.md is updated, **When** a developer reads User Story 4, **Then** they see it was superseded by the full-page navigation approach
2. **Given** spec.md is updated, **When** a developer reviews FR-006 (graceful degradation), **Then** it is noted that the baseline IS full-page navigation (no HTMX on cards), not a fallback
3. **Given** spec.md is updated, **When** a developer reviews edge cases, **Then** the "HTMX inline swap" edge case is updated to reflect it no longer applies

---

### Edge Cases

- **Other files reference `OnGetDetailAsync`**: Before removing the handler, verify no other views, tests, or documentation reference it. If found, update those references first.
- **Spec 004 depends on HTMX inline swap**: Spec 004 (`004-htmx-razor-conversion`) may reference the inline swap behavior. Its spec should be reviewed for consistency but is out of scope for this cleanup — note any cross-spec inconsistencies.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST remove the `OnGetDetailAsync` handler method from `src/Host/Pages/Courses/Detail.cshtml.cs`
- **FR-002**: System MUST verify no other file in the Host project references `OnGetDetailAsync` before removal
- **FR-003**: The removal MUST NOT break compilation or runtime behavior — full-page navigation via `asp-page` tag helpers must continue working
- **FR-004**: `specs/005-fix-view-details-navigation/tasks.md` MUST be updated so T004 and T005 descriptions match the actual implementation (HTMX removed, not modified)
- **FR-005**: `specs/005-fix-view-details-navigation/tasks.md` MUST annotate T008-T010 as superseded by the full-page navigation approach
- **FR-006**: `specs/005-fix-view-details-navigation/spec.md` MUST annotate US4 as intentionally abandoned with rationale
- **FR-007**: `specs/005-fix-view-details-navigation/spec.md` MUST update FR-006 (graceful degradation) to reflect that full-page navigation is the primary approach, not a fallback

### Key Entities

No new entities. This spec modifies:
- **Source code**: `Detail.cshtml.cs` (removal of one method)
- **Documentation**: `specs/005-fix-view-details-navigation/tasks.md` and `specs/005-fix-view-details-navigation/spec.md` (annotation updates)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero references to `OnGetDetailAsync` exist in the Host project source code after cleanup
- **SC-002**: Application builds and runs without errors after handler removal
- **SC-003**: Course catalog "View Details" and title links continue to navigate to full detail pages
- **SC-004**: tasks.md T004/T005 descriptions accurately describe the implementation that was done
- **SC-005**: spec.md US4 is annotated with the decision to use full-page navigation instead of HTMX inline swap

## Assumptions

- **No other callers exist**: The `OnGetDetailAsync` handler is only referenced by the HTMX attributes that were removed from `_CourseCard.cshtml`. No other view, partial, or JavaScript calls it.
- **Spec 004 is out of scope**: Any inconsistency with `specs/004-htmx-razor-conversion` is noted but not resolved in this spec. That spec may need its own follow-up.
- **No tests reference the handler**: Since no automated tests were written for spec 005, the handler has no test coverage to break.
- **This is documentation and dead-code cleanup only**: No behavioral changes to the running application are expected or desired.
