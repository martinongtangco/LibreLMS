# Tasks: Fix Course View Details Navigation

**Input**: Design documents from `/specs/005-fix-view-details-navigation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: No test tasks — this is a frontend navigation fix validated by manual browser scenarios (quickstart.md).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Review Existing Code)

**Purpose**: Understand current state before making changes.

- [x] T001 Review current `_CourseCard.cshtml` to identify all HTMX attributes on course title link and "View Details" button in `src/Host/Pages/Shared/_CourseCard.cshtml`
- [x] T002 [P] Review `Detail.cshtml` and `Detail.cshtml.cs` to confirm `OnGetAsync` renders full page correctly in `src/Host/Pages/Courses/Detail.cshtml`
- [x] T003 [P] Review `_CourseDetail.cshtml` partial to confirm it renders course content without layout wrapper in `src/Host/Pages/Shared/_CourseDetail.cshtml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No blocking prerequisites — the existing page structure is sound. This phase is skipped; proceed directly to user story tasks.

---

## Phase 3: User Story 1 - Click "View Details" Navigates to Course Detail Page (Priority: P1) 🎯 MVP

**Goal**: Fix the "View Details" button so it navigates to the full course detail page with layout, and the URL is bookmarkable (no `handler=` parameter).

**Independent Test**: Click "View Details" on any course card → full detail page renders with navbar/footer → URL shows clean path → refresh works.

### Implementation for User Story 1

- [x] T004 [US1] **Updated**: HTMX attributes (`hx-get`, `hx-target`, `hx-push-url`, `hx-swap`) were **removed** from the "View Details" button in `src/Host/Pages/Shared/_CourseCard.cshtml` and replaced with `asp-page="/Courses/Detail" asp-route-id="@Model.Id"` tag helper for full-page navigation. Rationale: simpler, more reliable, works without JavaScript, eliminates HTMX/full-page conflict.

**Checkpoint**: Clicking "View Details" performs a full-page navigation to `/Courses/Detail?id={guid}`. The page renders with the full layout (navbar, footer, etc.). Browser refresh works correctly.

---

## Phase 4: User Story 2 - Click Course Title Also Navigates to Detail Page (Priority: P1)

**Goal**: The course title link (heading in the card) has the same fix as the "View Details" button — both navigate to the detail page with bookmarkable URLs.

**Independent Test**: Click the course title link → same result as clicking "View Details".

### Implementation for User Story 2

- [x] T005 [P] [US2] **Updated**: HTMX attributes were **removed** from the course title link in `src/Host/Pages/Shared/_CourseCard.cshtml` and replaced with `asp-page="/Courses/Detail" asp-route-id="@Model.Id"` tag helper for full-page navigation (same approach as T004).

**Checkpoint**: Both the title link and "View Details" button perform full-page navigation to the course detail page and render correctly on refresh.

---

## Phase 5: User Story 3 - Direct URL Access to Course Detail Works (Priority: P2)

**Goal**: Confirm that navigating directly to `/Courses/Detail?id={guid}` renders the full page with layout. No code changes needed — this is validated by confirming the existing `OnGetAsync` handler works correctly.

**Independent Test**: Navigate to `/Courses/Detail?id={guid}` directly → full page renders. Navigate to a non-existent GUID → "Course Not Found" with layout.

### Implementation for User Story 3

- [x] T006 [US3] Verify `OnGetAsync` in `src/Host/Pages/Courses/Detail.cshtml.cs` correctly handles missing courses (sets `Course` to null when `GetByIdAsync` returns null) so the "Course Not Found" state renders with layout
- [x] T007 [P] [US3] Verify `Detail.cshtml` renders the "Course Not Found" state with full layout when `Model.Course is null` in `src/Host/Pages/Courses/Detail.cshtml`

**Checkpoint**: Direct URL access works for both valid and invalid course IDs. The detail page always renders with the full layout.

---

## Phase 6: User Story 4 - HTMX Inline Swap Works with Clean URL (Priority: P3)

**Goal**: Confirm HTMX inline swap loads the detail partial into `#main-content` and the URL is clean for bookmarks/refresh.

**Independent Test**: With HTMX loaded, click a course card → partial loads inline → URL updates to clean path → refresh renders full page.

### Implementation for User Story 4

> **SUPERSEDED** (by spec 006 cleanup): HTMX inline swap from the course card was intentionally abandoned in favor of full-page navigation via `asp-page` tag helpers. The `OnGetDetailAsync` handler these tasks reference no longer has callers and has been removed. These verification tasks are no longer applicable.

- [x] T008 [P] [US4] ~~Verify `OnGetDetailAsync` in `src/Host/Pages/Courses/Detail.cshtml.cs` returns `Partial("_CourseDetail", model)`~~ — **SUPERSEDED**: `OnGetDetailAsync` has been removed (no HTMX callers remain)
- [x] T009 [P] [US4] ~~Verify `#main-content` div exists in `src/Host/Pages/Courses/Index.cshtml`~~ — **SUPERSEDED**: HTMX inline swap from course cards is no longer used
- [x] T010 [US4] ~~Verify graceful degradation~~ — **SUPERSEDED**: Full-page navigation IS the primary approach; no HTMX on course card links. HTMX remains only for catalog filtering.

**Checkpoint**: ~~HTMX inline swap works, URL is clean, and disabling JavaScript falls back to full-page navigation via `href`.~~ — N/A (US4 abandoned, full-page navigation is the approach).

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Ensure no regressions and all validation scenarios pass.

- [x] T011 Run all 8 validation scenarios from `specs/005-fix-view-details-navigation/quickstart.md` and confirm pass
- [x] T012 [P] Verify existing HTMX catalog filtering (search, category dropdown) still works on `src/Host/Pages/Courses/Index.cshtml` after `_CourseCard.cshtml` changes
- [x] T013 [P] Verify "My Courses" page links to course detail pages correctly (no `handler=` URL): check `src/Host/Pages/Shared/_MyCourseRow.cshtml` for the same `hx-push-url` pattern that was broken in `_CourseCard.cshtml` — this page uses a similar HTMX link structure and is a regression risk
- [x] T014 Verify `_CourseDetail.cshtml` partial's "Back to Catalog" link uses the correct HTMX swap back to catalog in `src/Host/Pages/Shared/_CourseDetail.cshtml`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Story 1 (Phase 3)**: Depends on Setup — fix the "View Details" button
- **User Story 2 (Phase 4)**: Depends on Setup — same file as US1, apply fix to title link
- **User Story 3 (Phase 5)**: Independent verification — no code changes expected
- **User Story 4 (Phase 6)**: Depends on US1/US2 fixes — verify HTMX inline swap with clean URL
- **Polish (Phase 7)**: Depends on all user stories — regression testing

### User Story Dependencies

- **US1 (P1)**: Core fix — `hx-push-url` on "View Details" button
- **US2 (P1)**: Same fix applied to title link — same file, can be done with US1
- **US3 (P2)**: Verification only — no code changes
- **US4 (P3)**: Verification of HTMX behavior after US1/US2 fixes

### Parallel Opportunities

- T002 and T003 can run in parallel (reviewing different files)
- T004 and T005 can be done together (same file, adjacent elements)
- T006 and T007 can run in parallel (different files)
- T008, T009, T010 can run in parallel (different files)
- T012, T013, T014 can run in parallel (regression checks on different files)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Review existing code
2. Complete Phase 3: Fix `hx-push-url` on "View Details" button (T004)
3. **STOP and VALIDATE**: Run quickstart scenarios 1, 3, 4 (View Details → bookmark → refresh)
4. If all pass, the core bug is fixed

### Incremental Delivery

1. T004 → US1 MVP done (View Details button fixed)
2. T005 → US2 done (title link fixed too)
3. T006, T007 → US3 verified (direct URL works)
4. T008-T010 → US4 verified (HTMX swap + graceful degradation)
5. T011-T014 → Polish (regression checks, all quickstart scenarios pass)

### Key Change Summary

The core fix is **one attribute change per link** in `_CourseCard.cshtml`:

```
# Before (broken):
hx-push-url="true"
# Pushes: /Courses/Detail?id={guid}&handler=Detail → broken on refresh

# After (fixed):
hx-push-url="/Courses/Detail?id=@Model.Id"
# Pushes: /Courses/Detail?id={guid} → full page on refresh
```

This change is applied to both the title link and the "View Details" button (2 elements, 1 file).

---

## Notes

- This is a minimal-change fix: 1 file modified, 2 attribute changes
- No server-side code changes required (confirmed by research.md)
- No new files, no new endpoints, no new entities
- All validation is manual browser testing (quickstart.md scenarios)
- Regression testing ensures existing HTMX catalog filtering is not broken
