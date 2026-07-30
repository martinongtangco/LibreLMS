# Tasks: Course Launch & Status Tracking

**Input**: Design documents from `/specs/007-course-launch-status/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/status-mapping.md

**Tests**: Not included — the feature specification does not request test tasks. Existing test projects (`Scorm.Tests`, `Enrollment.Tests`, `ArchitectureTests`) validate module boundaries and existing SCORM flows.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Branch creation and project preparation

- [ ] T001 Create branch `story/007-course-launch-status` from `main` per Constitution Principle VIII

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core display utilities that ALL user stories depend on. Must complete before any story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 Add `GetDisplayLabel(string rawStatus)` method to `src/Host/ScormHelpers.cs` — maps SCORM 1.2 `cmi.core.lesson_status` values to human-readable labels: "not attempted" → "Not Started", "incomplete" → "In Progress", "completed" → "Completed", "passed" → "Passed", "failed" → "Failed", "browsed" → "Browsed", "neutral" → "Not Started", legacy "in-progress" → "In Progress", "abandoned" → "Abandoned"; unknown values pass through unchanged
- [ ] T003 [P] Add `GetDisplayPercentage(double? scoreRaw)` method to `src/Host/ScormHelpers.cs` — returns "N/A" when scoreRaw is null, "{score}%" for values 0–100 (integer, no decimal places)
- [ ] T004 [P] Add `GetStatusBadgeColors(string rawStatus)` method to `src/Host/ScormHelpers.cs` — returns CSS color hints (background + text color) per status category: success (green #e8f5e9/#2e7d32) for completed/passed, warning (orange #fff3e0/#e65100) for in-progress/incomplete/abandoned, error (red #ffebee/#c62828) for failed, neutral (gray #f5f5f5/#666) for not-started/browsed/neutral

**Checkpoint**: Foundation ready — status mapping, percentage formatting, and color utilities are available for all UI views.

---

## Phase 3: User Story 1 + 2 — Launch Course + View SCORM Status (Priority: P1) 🎯 MVP

**Goal**: Students can launch enrolled SCORM courses and see all 6 SCORM 1.2 status values displayed correctly with human-readable labels on the "My Courses" page.

**Independent Test**: Enroll a student in a SCORM course, click Launch, verify status changes to "In Progress". Manually set various lesson_status values (via test data or SCORM content) and verify each displays the correct label on `/MyCourses`.

### Implementation for US1 + US2

- [ ] T005 [US1] [US2] Update `_MyCourseRow.cshtml` at `src/Host/Pages/Shared/_MyCourseRow.cshtml` — replace the inline switch expression with calls to `ScormHelpers.GetDisplayLabel()` and `ScormHelpers.GetStatusBadgeColors()`. Handle null `LatestStatus` as "Not Started". Show the display label badge with appropriate colors for all SCORM status values.
- [ ] T006 [US1] Update `MyCourses/Index.cshtml.cs` at `src/Host/Pages/MyCourses/Index.cshtml.cs` — ensure the `EnrollmentRow` record passes through the raw `LatestStatus` and `LatestScore` values (no transformation needed; display mapping is handled by the partial view). Verify the HTMX refresh handler returns the updated partial with new status mapping.
- [ ] T007 [US1] Verify launch status transition in `src/Modules/Scorm/Application/ScormSessionService.cs` — confirm that `LaunchAsync` creates `CourseAttempt` with `Status = "in-progress"`, which maps to "In Progress" display label. No code change needed if confirmed, but add a comment documenting the SCORM mapping intent.
- [ ] T008 [US2] Update `MyCourses/Index.cshtml` at `src/Host/Pages/MyCourses/Index.cshtml` — no structural changes needed; verify HTMX refresh flow works with updated `_MyCourseRow.cshtml`.

**Checkpoint**: At this point, US1 and US2 are both functional. Students can launch courses and see all 6 SCORM status values with correct labels and colors on the enrolled courses page.

---

## Phase 4: User Story 3 — View Percentage Completion (Priority: P2)

**Goal**: Students see percentage completion alongside status for each enrolled course, derived from `cmi.core.score.raw`. Courses with no score show "N/A".

**Independent Test**: Set various `cmi.core.score.raw` values (0, 50, 85, 100, null) via SCORM content or test data and verify the enrolled courses view displays the correct percentage.

### Implementation for US3

- [ ] T009 [US3] Update `_MyCourseRow.cshtml` at `src/Host/Pages/Shared/_MyCourseRow.cshtml` — add percentage completion display alongside the status badge using `ScormHelpers.GetDisplayPercentage(Model.LatestScore)`. Show "N/A" when LatestScore is null, "{score}%" for numeric values.
- [ ] T010 [US3] Fix score=0 persistence bug in `src/Modules/Scorm/Application/ScormSessionService.cs` — in `CommitAsync()`, change `if (double.TryParse(sessionData.CmiScoreRaw, out var score) && score > 0)` to `if (double.TryParse(sessionData.CmiScoreRaw, out var score) && score >= 0)` so that a legitimate score of 0 is saved to `CourseAttempt.ScoreRaw`. Apply the same fix in `FinishAsync()`.

**Checkpoint**: At this point, US3 is functional. Students see percentage completion for all courses, including 0% for failed courses and "N/A" for courses without a score.

---

## Phase 5: User Story 4 — Status Updates During and After Session (Priority: P2)

**Goal**: Status and score updates during SCORM sessions are correctly persisted on `LMSCommit`/`LMSFinish` and reflected immediately on the enrolled courses page.

**Independent Test**: Launch a course, set `lesson_status` to "failed" via SCORM content, call `LMSCommit`, then navigate to `/MyCourses` and verify the status shows "Failed" with the correct score.

### Implementation for US4

- [ ] T011 [US4] Verify `CommitAsync()` in `src/Modules/Scorm/Application/ScormSessionService.cs` — confirm that the method reads `CmiLessonStatus` from Valkey session and writes it directly to `CourseAttempt.Status`. This ensures SCORM standard values (not custom values) are persisted. No code change needed if confirmed; add a comment documenting the behavior.
- [ ] T012 [US4] Verify `FinishAsync()` in `src/Modules/Scorm/Application/ScormSessionService.cs` — confirm same behavior: `CmiLessonStatus` and `CmiScoreRaw` from Valkey are written to `CourseAttempt`. The score=0 fix from T010 must be applied here too.
- [ ] T013 [US4] Update `MyCourses/Index.cshtml.cs` at `src/Host/Pages/MyCourses/Index.cshtml.cs` — verify that the HTMX refresh handler (`OnGetEnrollmentsAsync`) re-queries `ScormAttemptService.GetMyAttemptsAsync()` to get the latest committed status after each session end. Confirm the latest attempt per course is selected (by `AttemptNumber` descending).

**Checkpoint**: All user stories are now functional. Status updates persist correctly through commit/finish, and the enrolled courses page reflects the latest attempt status and score.

---

## Phase 6: Course Detail Page Enhancement

**Goal**: The course detail page (`/Courses/Detail/{id}`) shows status and percentage for enrolled students, consistent with the My Courses page.

**Independent Test**: Navigate to a course detail page as an enrolled student with a completed attempt — verify the page shows the course status, percentage, and a "Launch" button.

### Implementation for detail page

- [ ] T014 Inject `ScormAttemptService` into `CourseDetailModel` at `src/Host/Pages/Courses/Detail.cshtml.cs` — add it as a constructor dependency alongside existing services.
- [ ] T015 Update `CourseDetailModel.OnGetAsync()` at `src/Host/Pages/Courses/Detail.cshtml.cs` — when the student is enrolled, query `ScormAttemptService.GetMyAttemptsAsync(studentId)` for this course, select the latest attempt, and expose `LatestStatus` (string?) and `LatestScore` (double?) on the model.
- [ ] T016 Update `CourseDetailItem` record at `src/Host/Pages/Courses/Detail.cshtml.cs` — add optional `LatestStatus` and `LatestScore` properties.
- [ ] T017 Update `Courses/Detail.cshtml` at `src/Host/Pages/Courses/Detail.cshtml` — in the enrolled section (below the "✓ Enrolled" badge), display the course status using `ScormHelpers.GetDisplayLabel()` and percentage using `ScormHelpers.GetDisplayPercentage()`. Show the status badge and percentage alongside the Launch button.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and consistency checks.

- [ ] T018 Verify status display consistency between `MyCourses/Index.cshtml` and `Courses/Detail.cshtml` — both views must show identical status labels, colors, and percentages for the same enrollment/attempt data.
- [ ] T019 [P] Run architecture tests to confirm no module boundary violations: `dotnet test tests/ArchitectureTests`
- [ ] T020 [P] Run quickstart.md validation scenarios — walk through all 9 scenarios in `specs/007-course-launch-status/quickstart.md` to verify end-to-end correctness
- [ ] T021 Build and verify: `dotnet build` with zero warnings

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1+US2 (Phase 3)**: Depends on Foundational — MVP deliverable
- **US3 (Phase 4)**: Depends on Foundational + US2 (shares `_MyCourseRow.cshtml`)
- **US4 (Phase 5)**: Depends on US1 (references `ScormSessionService`)
- **Detail Page (Phase 6)**: Depends on Foundational (uses `ScormHelpers` utilities)
- **Polish (Phase 7)**: Depends on all story phases

### User Story Dependencies

- **US1 + US2 (P1)**: Bundled together — launch flow and status display are tightly coupled in the same partial view
- **US3 (P2)**: Depends on US2's partial view update (adds percentage to the same row)
- **US4 (P2)**: Depends on US1 (session service changes) but is independent from US3
- **Detail Page (Phase 6)**: Independent of all stories — can run in parallel with Phase 4+5

### Within Each User Story

- Foundational utilities (T002-T004) before any UI changes
- Service-layer fixes (T010) before view-layer changes that depend on them
- UI updates before consistency verification (T018)

### Parallel Opportunities

- T002, T003, T004 (Phase 2): All add methods to the same file but are independent additions — can be done in a single edit pass
- T005 (US1/US2 row update) is the single critical path task in Phase 3
- T010 (score=0 fix) can start in parallel with T009 (percentage display) — different files
- T014-T017 (detail page) can run in parallel with Phase 4/5

---

## Parallel Example: Phase 2 (Foundational)

```
# All three utility methods can be added in one edit to ScormHelpers.cs:
Task T002: Add GetDisplayLabel() to src/Host/ScormHelpers.cs
Task T003: Add GetDisplayPercentage() to src/Host/ScormHelpers.cs
Task T004: Add GetStatusBadgeColors() to src/Host/ScormHelpers.cs
```

## Parallel Example: Phase 4 + 5

```
# These touch different files and can proceed in parallel:
Task T009: Update _MyCourseRow.cshtml (percentage display)
Task T010: Fix score=0 bug in ScormSessionService.cs
Task T011: Verify CommitAsync persistence in ScormSessionService.cs
Task T014-T017: Update course detail page (Detail.cshtml.cs + Detail.cshtml)
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1: Create branch
2. Complete Phase 2: Foundational utilities (T002-T004)
3. Complete Phase 3: Launch + Status display (T005-T008)
4. **STOP and VALIDATE**: Student can launch a course and see all 6 SCORM status labels
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Utilities ready
2. US1+US2 → Launch + Status (MVP!) → Test independently
3. US3 → Percentage completion → Test independently
4. US4 → Session persistence fix → Test independently
5. Detail page → Consistent display everywhere → Test independently
6. Polish → Architecture tests + quickstart validation

### Parallel Team Strategy

With multiple developers:

1. Complete Phase 2 together (all touch `ScormHelpers.cs`)
2. Once Foundational is done:
   - Developer A: Phase 3 (US1+US2, the row partial)
   - Developer B: Phase 6 (Course detail page enhancement)
3. After Phase 3 merges:
   - Developer A: Phase 4 (US3 percentage)
   - Developer A: Phase 5 (US4 session persistence)
4. All merge → Phase 7 (Polish)

---

## Notes

- [P] tasks = different files or independent additions, no merge conflicts expected
- [Story] label maps task to specific user story for traceability
- All tasks target existing files — no new files are created (except adding methods to ScormHelpers.cs)
- Commit after each task or logical group
- Stop at any checkpoint to validate independently
- Architecture tests (`dotnet test tests/ArchitectureTests`) must pass before merging
