# Tasks: Rebrand to Libre LMS

**Input**: Design documents from `/specs/008-rebrand-libre-lms/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

**Tests**: Not requested in specification — no test tasks included.

**Organization**: Tasks are grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Preparation — no new infrastructure needed for a rebrand

- [ ] T001 Create git branch `story/008-rebrand-libre-lms` from `main`

---

## Phase 2: User Story 1 - UI Branding Update (Priority: P1) 🎯 MVP

**Goal**: All user-facing text shows "Libre LMS" instead of "Learning LMS"

**Independent Test**: Open the application in a browser; verify page title, navbar brand, and footer all show "Libre LMS" with zero instances of "Learning LMS"

### Implementation

- [ ] T002 [P] [US1] Update `src/Host/Pages/Shared/_Layout.cshtml`: change `<title>` from "Learning LMS" to "Libre LMS"
- [ ] T003 [P] [US1] Update `src/Host/Pages/Shared/_Layout.cshtml`: change `.brand` span from "Learning LMS" to "Libre LMS"
- [ ] T004 [P] [US1] Update `src/Host/Pages/Shared/_Layout.cshtml`: change footer copyright from "Learning LMS" to "Libre LMS"
- [ ] T005 [P] [US1] Update `src/Host/Pages/Scorm/Launch.cshtml`: change page title from "Learning LMS" to "Libre LMS"

**Checkpoint**: UI branding complete — browser shows "Libre LMS" everywhere

---

## Phase 3: User Story 2 - Documentation Update (Priority: P2)

**Goal**: All documentation references the application as "Libre LMS"

**Independent Test**: Grep documentation files for "Learning LMS" — should return zero matches

### Implementation

- [ ] T006 [P] [US2] Update `README.md`: replace all instances of "Learning LMS" with "Libre LMS" and "LearningLms" with "LibreLms"
- [ ] T007 [P] [US2] Update `.specify/memory/constitution.md`: replace title and body references of "Learning LMS" with "Libre LMS"

**Checkpoint**: Documentation complete — no "Learning LMS" in README or constitution

---

## Phase 4: User Story 3 - Internal Naming Update (Priority: P3)

**Goal**: All internal identifiers use "LibreLms" namespace

**Independent Test**: `grep -r "LearningLms" src/ --include="*.cs" --exclude-dir=Migrations` returns zero results

### Implementation

- [ ] T008 [P] [US3] Rename solution file: `LearningLms.slnx` → `LibreLms.slnx`
- [ ] T009 [P] [US3] Update `src/Host/appsettings.Development.json`: change database name from "LearningLms" to "LibreLms"
- [ ] T010 [P] [US3] Rename namespaces in `src/SharedKernel/`: Entity.cs, IDomainEvent.cs, Result.cs (`LearningLms` → `LibreLms`)
- [ ] T011 [P] [US3] Rename namespaces in `src/Host/`: Program.cs, ScormHelpers.cs, and all Pages/*.cshtml.cs files
- [ ] T012 [P] [US3] Rename namespace in `src/Host/Pages/Error.cshtml` (@using directive)
- [ ] T013 [P] [US3] Rename namespaces in `src/Modules/Catalog/`: all files in Application/, Domain/, Endpoints/, Infrastructure/, ModuleMarker.cs
- [ ] T014 [P] [US3] Rename namespaces in `src/Modules/Catalog.Contracts/`: all .cs files
- [ ] T015 [P] [US3] Rename namespaces in `src/Modules/Enrollment/`: all files in Application/, Domain/, Endpoints/, Infrastructure/, ModuleMarker.cs
- [ ] T016 [P] [US3] Rename namespaces in `src/Modules/Enrollment.Contracts/`: all .cs files
- [ ] T017 [P] [US3] Rename namespaces in `src/Modules/Scorm/`: all files in Application/, Domain/, Endpoints/, Infrastructure/, ModuleMarker.cs
- [ ] T018 [P] [US3] Rename namespaces in `src/Modules/Scorm.Contracts/`: all .cs files
- [ ] T019 [P] [US3] Rename namespaces in `tests/ArchitectureTests/`: ModuleBoundaryTests.cs
- [ ] T020 [P] [US3] Rename namespaces in `tests/Catalog.Tests/`: PlaceholderTests.cs
- [ ] T021 [P] [US3] Rename namespaces in `tests/Enrollment.Tests/`: PlaceholderTests.cs
- [ ] T022 [P] [US3] Rename namespaces in `tests/Scorm.Tests/`: PlaceholderTests.cs

**Checkpoint**: Internal naming complete — all namespaces use "LibreLms"

---

## Phase 5: Validation & Polish

**Purpose**: Verify the rebrand is complete and the application builds and runs correctly

- [ ] T023 Build solution: `dotnet build LibreLms.slnx` — must succeed with zero errors
- [ ] T024 Run architecture tests: `dotnet test tests/ArchitectureTests` — must pass
- [ ] T025 Run all tests: `dotnet test LibreLms.slnx` — must pass
- [ ] T026 Verify no "LearningLms" in active source: `grep -r "LearningLms" src/ --include="*.cs" --exclude-dir=Migrations` — must be empty
- [ ] T027 Verify no "Learning LMS" in documentation: `grep -r "Learning LMS" README.md .specify/memory/constitution.md` — must be empty
- [ ] T028 [P] Run application and verify UI: `dotnet run --project src/Host` — check browser for "Libre LMS" branding
- [ ] T029 Commit all changes with descriptive commit message

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — create branch first
- **US1 (Phase 2)**: No dependencies on other stories — can start immediately after branch creation
- **US2 (Phase 3)**: No dependencies on other stories — can run in parallel with US1
- **US3 (Phase 4)**: No dependencies on other stories — can run in parallel with US1 and US2
- **Validation (Phase 5)**: Depends on all three user stories being complete

### Parallel Opportunities

- T002, T003, T004, T005 (US1) can all be done in a single file edit
- T006, T007 (US2) can run in parallel with US1 tasks
- T008–T022 (US3) are all parallelizable across different files — the bulk of the work
- All namespace renames (T010–T022) can be done in parallel as they touch different files

### Recommended Execution Order

1. T001 — Create branch
2. T002–T005 — UI changes (quick, visible impact)
3. T006–T007 — Documentation changes
4. T008–T009 — Config and solution file
5. T010–T022 — Namespace renames (bulk of work, all parallel)
6. T023–T029 — Validation and commit

---

## Notes

- Migration files in `src/Host/Migrations/` are explicitly excluded — do NOT change them
- Historical spec directories (001–007) are explicitly excluded — do NOT change them
- The `@using` directive in `Error.cshtml` (T012) is a Razor directive, not a C# namespace — requires careful editing
- Use IDE refactoring (Rename Symbol) for C# namespaces where possible to ensure all `using` directives update automatically
