# Feature Specification: SCORM Launch & Completion

**Feature Branch**: `002-scorm-launch-completion`

**Created**: 2025-07-29

**Status**: Complete (merged 2026-07-28)

**Input**: User description: "Slice 2: Scorm Launch & Completion"

## Clarifications

### Session 2025-07-29

- Q: Session interruption behavior — what happens when a student closes the browser tab mid-session? → A: Auto-commit on tab close via client-side `beforeunload` handler (standard SCORM behavior)
- Q: Concurrent session handling — what happens if a student opens the same course in a second tab? → A: Reject second launch, show "session already active" message
- Q: Score boundary handling — what happens when score.raw is outside 0-100? → A: Reject out-of-range values, return SCORM error code (spec-compliant)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Launch a SCORM Course (Priority: P1)

An enrolled student navigates to a course that is a SCORM package, clicks "Launch," and the system opens the SCORM course content in their browser with a live session initialized. The student sees the course's first screen as defined by the package manifest.

**Why this priority**: Without the ability to launch a course, none of the SCORM tracking, progress, or completion features have any value. This is the entry point for the entire learning experience.

**Independent Test**: A student enrolled in a SCORM course clicks Launch and sees the course content rendered in the browser. The session is initialized and ready for SCORM API calls.

**Acceptance Scenarios**:

1. **Given** a student is enrolled in a SCORM course, **When** they click "Launch" on the course detail page, **Then** the SCORM player page loads showing the course's start content with the session initialized
2. **Given** a student is not enrolled in a SCORM course, **When** they attempt to launch it, **Then** they see a message prompting them to enroll first
3. **Given** a SCORM package is missing or its manifest is unreadable, **When** a student attempts to launch it, **Then** they see an error message indicating the course content is unavailable

---

### User Story 2 - Track Course Progress During a Session (Priority: P1)

As a student interacts with SCORM content (answering questions, navigating screens), the system captures progress updates from the SCORM runtime API (`LMSSetValue` calls) and maintains the current session state. The student can see their current status (e.g., "incomplete", "completed") reflected in the system.

**Why this priority**: Progress tracking is core to the LMS value proposition. Without it, the system cannot determine completion or provide meaningful status feedback. Combined with US1, this delivers the essential SCORM experience.

**Independent Test**: During a live SCORM session, `LMSSetValue` calls for `cmi.core.lesson_status` and `cmi.suspend_data` are captured and can be read back via `LMSGetValue`. Session state is maintained across page interactions within the session.

**Acceptance Scenarios**:

1. **Given** a SCORM session is active, **When** the course content calls `LMSSetValue("cmi.core.lesson_status", "completed")`, **Then** the session state is updated and `LMSGetValue("cmi.core.lesson_status")` returns "completed"
2. **Given** a SCORM session is active, **When** the course content sets suspend data via `LMSSetValue("cmi.suspend_data", "...")`, **Then** the data is stored and retrievable within the same session
3. **Given** a SCORM session is active, **When** the student navigates between course screens, **Then** the session remains active and all previously set values are preserved

---

### User Story 3 - Commit Completion and View Results (Priority: P2)

When a student finishes or exits a SCORM course (via `LMSCommit` or `LMSFinish`), the system durably saves the final state (completion status, score, elapsed time) and the student can see their completed courses and results on a dashboard.

**Why this priority**: Committing results is what makes the learning permanent. Without durable storage of completion data, progress is lost on session end. This is necessary for any reporting or certification use case.

**Independent Test**: After a student completes a SCORM course and the session ends, the completion record persists across page reloads and can be viewed in the "My Courses" section with status, score, and date.

**Acceptance Scenarios**:

1. **Given** a student has completed a SCORM session with `lesson_status` set to "completed" and a score of 85, **When** the session ends via `LMSFinish`, **Then** the completion record is saved with status "completed", score 85, and the session duration
2. **Given** a student has previously completed a SCORM course, **When** they view their enrolled courses, **Then** they see the course marked as completed with their score and completion date
3. **Given** a student abandoned a SCORM session without completing, **When** they view their enrolled courses, **Then** the course shows as "In Progress" or "Not Started" (depending on whether any progress was committed)

---

### User Story 4 - Resume a Course from Last Checkpoint (Priority: P3)

A student who previously started but did not complete a SCORM course can relaunch it and resume from their last committed checkpoint (last `LMSCommit`/`LMSFinish` data), including restored suspend data (e.g., last page position).

**Why this priority**: Resuming improves user experience for longer courses but is not required for the basic complete-a-course flow. It builds on the committed data from US3.

**Independent Test**: A student starts a course, commits progress (e.g., via `LMSCommit` mid-session), exits, and relaunches — the course restores from the committed checkpoint with suspend data available.

**Acceptance Scenarios**:

1. **Given** a student previously committed progress in a SCORM course (e.g., `cmi.suspend_data` with a bookmark), **When** they relaunch the course, **Then** `LMSGetValue("cmi.suspend_data")` returns the previously saved bookmark data
2. **Given** a student previously completed a course, **When** they relaunch it, **Then** they are informed the course is already completed and can choose to retake or view results
3. **Given** a student started a course but never committed progress (session lost before `LMSCommit`), **When** they relaunch the course, **Then** a fresh session starts with no prior data restored

---

### User Story 5 - Upload a SCORM Package (Priority: P3)

An admin or instructor can upload a SCORM 1.2 package (ZIP file containing `imsmanifest.xml` and content files) so it becomes available as a launchable course in the catalog.

**Why this priority**: Package upload enables the system to ingest new courses but is a management/admin concern. For the student-facing MVP (US1-US3), courses can be pre-seeded with sample SCORM content.

**Independent Test**: An admin uploads a valid SCORM 1.2 ZIP package, and the course becomes visible in the catalog and launchable by enrolled students.

**Acceptance Scenarios**:

1. **Given** an admin uploads a valid SCORM 1.2 ZIP package containing `imsmanifest.xml`, **When** the upload completes, **Then** the course appears in the catalog as a launchable SCORM course
2. **Given** an admin uploads a ZIP that is not a valid SCORM package (missing manifest), **When** the upload is processed, **Then** they see an error explaining the package is invalid
3. **Given** a SCORM package has been uploaded, **When** an enrolled student launches it, **Then** the correct content from the package is served

---

### Edge Cases

- **Concurrent sessions**: If a student opens the same SCORM course in a second browser tab while a session is active, the second launch is rejected with a "session already active" message. The student must finish or close the first session before launching again.
- **Session timeout**: What happens if a student leaves a SCORM session open for an extended period without activity?
- **Tab close mid-session**: When a student closes the browser tab, the client-side `beforeunload` handler triggers `LMSCommit()` to auto-save current progress before the session ends.
- **Partial completion on crash**: If the system restarts during a session, in-progress Valkey state is lost — the student resumes from the last committed checkpoint.
- **Score boundaries**: Scores above 100 or below 0 are rejected — `LMSSetValue("cmi.core.score.raw", value)` returns `false` and `LMSGetValue("cmi.core.error_code")` returns a non-zero error code, per SCORM 1.2 spec.
- **Multiple attempts**: A student may attempt the same course multiple times — each attempt creates a separate record.
- **Manifest with multiple SCOs**: The system uses the first SCO or the one marked with `main` as the launch point; sequencing beyond that is out of scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow an enrolled student to launch a SCORM course, initializing a live session
- **FR-002**: System MUST serve SCORM content files (HTML, JS, CSS, media) from the uploaded package
- **FR-003**: System MUST expose the SCORM runtime API (`LMSInitialize`, `LMSFinish`, `LMSGetValue`, `LMSSetValue`, `LMSCommit`) so that standard SCORM 1.2 content packages can communicate with the system
- **FR-004**: System MUST support the following CMI fields: `cmi.core.student_id`, `cmi.core.student_name`, `cmi.core.lesson_status`, `cmi.core.credit`, `cmi.core.entry`, `cmi.core.exit`, `cmi.core.score.raw`, `cmi.core.session_time`, and `cmi.suspend_data`
- **FR-005**: System MUST store live SCORM session state (the `cmi.*` key/value bag) in an ephemeral store during an active session
- **FR-006**: System MUST persist completed session data (status, score, elapsed time) to durable storage on `LMSCommit()` or `LMSFinish()`
- **FR-007**: System MUST trigger an automatic `LMSCommit()` via a client-side `beforeunload` handler when the student closes the browser tab, preserving in-progress session state
- **FR-008**: System MUST prevent a student from launching a SCORM course without a valid enrollment
- **FR-009**: System MUST prevent a student from launching a second concurrent session for the same course — reject with a "session already active" message
- **FR-010**: System MUST display completion status and score for each course on the student's enrolled courses view
- **FR-011**: System MUST restore committed checkpoint data (including `cmi.suspend_data`) when a student resumes a course from a prior attempt
- **FR-012**: System MUST handle multiple attempts per course, keeping a record of each attempt's outcome
- **FR-013**: System MUST allow an admin to upload a SCORM 1.2 package as a ZIP file containing `imsmanifest.xml`
- **FR-014**: System MUST validate uploaded SCORM packages and reject invalid packages with a clear error message
- **FR-015**: System MUST default `cmi.core.lesson_status` to "not attempted" at session start and update it to "incomplete" on first interaction, "completed" when the content sets it

### Key Entities

- **ScormPackage**: Represents an uploaded SCORM 1.2 package — includes manifest metadata, content file path, title, and launch SCO identifier. Linked to a `Course` in the Catalog module.
- **CourseAttempt**: Represents a single student's attempt at a SCORM course — includes student ID, course ID, session status (in-progress, completed, abandoned), score, elapsed time, and the commit timestamp. Multiple attempts per student/course are allowed.
- **ScormSession**: Ephemeral state for an active SCORM session — the live `cmi.*` key/value bag (student_id, lesson_status, suspend_data, session_time, etc.) stored temporarily during the session and written to a `CourseAttempt` on commit/finish. A client-side `beforeunload` handler ensures auto-commit on tab close.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An enrolled student can launch a SCORM course and see the content within 3 seconds
- **SC-002**: SCORM API calls (`LMSGetValue`, `LMSSetValue`) respond within 500ms during an active session
- **SC-003**: Completion data (status, score, time) is durably saved within 1 second of `LMSFinish()` or `LMSCommit()`
- **SC-004**: A student can view their course completion status and score on the "My Courses" page immediately after finishing a session
- **SC-005**: Resume flow restores suspend data (bookmark position) from the last committed checkpoint with 100% accuracy
- **SC-006**: Invalid SCORM packages are rejected at upload time with a clear error message, preventing broken courses from appearing in the catalog
- **SC-007**: The system correctly handles the full set of 9 required CMI fields without data loss or type errors during a session

## Assumptions

- **SCORM 1.2 only**: The system supports SCORM 1.2 simplified — SCORM 2004, multi-SCO sequencing, and `cmi.interactions` tracking are out of scope.
- **Single-user testing**: The primary scenario is a single student interacting with a course. Concurrent session handling is addressed as an edge case but not a primary design driver.
- **Razor Pages web portal**: The student-facing UI (launch, resume, completion view) is built using Razor Pages, consistent with the existing catalog and enrollment UI.
- **Existing authentication reused**: Student identity is established via the same authentication mechanism already in place from Slice 1 (cookie/JWT auth).
- **Valkey is available**: The Valkey (Redis-protocol) container defined in `docker-compose.yml` is running and accessible for ephemeral session storage, as described in ADR-0003.
- **Catalog module provides course data**: The Scorm module accesses course information (title, existence) through `Catalog.Contracts` (`ICourseLookup`), consistent with the compiled module boundary principle.
- **SCORM content files are served statically**: Package content files are extracted to disk and served as static files; no special proxy or streaming is needed.
- **Admin role exists**: There is at least one user with admin/instructor privileges who can upload SCORM packages. The specific role mechanism is an implementation detail to be decided in planning.
