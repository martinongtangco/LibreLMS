# Feature Specification: Course Launch & Status Tracking

**Feature Branch**: `story/007-course-launch-status`

**Created**: 2025-07-30

**Status**: Draft

**Input**: User description: "we need to implement the launch course once enrolled. it needs to be able to show the status of the course if its launched, in progress, failed, complete (see correct status aligned to SCORM standards). if we can provide percentage completion (see SCORM standards), then lets also specify that."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Launch an Enrolled Course (Priority: P1)

An enrolled student navigates to their enrolled courses list or a course detail page and clicks "Launch" to start the SCORM course. The system validates that the student has an active enrollment, initializes a SCORM session, and presents the course content in the browser. The course status transitions from "Not Started" to "In Progress."

**Why this priority**: This is the primary action students take after enrollment. Without a working launch flow, enrollment has no value and no other status or completion features can be exercised.

**Independent Test**: Can be fully tested by enrolling a student in a SCORM course, clicking Launch, and verifying the course content loads with the status updated to "In Progress."

**Acceptance Scenarios**:

1. **Given** a student is enrolled in a SCORM course with no prior attempts, **When** they click "Launch," **Then** the SCORM session initializes and the course content loads, and the enrollment status changes from "Not Started" to "In Progress"
2. **Given** a student is enrolled but has no valid SCORM package associated with the course, **When** they click "Launch," **Then** they see an error message indicating the course content is unavailable
3. **Given** a student is not enrolled in a course, **When** they attempt to launch it, **Then** they see a message prompting them to enroll first
4. **Given** a student already has an active session for the course (e.g., another browser tab), **When** they click "Launch" again, **Then** they see a message that a session is already active and cannot start a duplicate

---

### User Story 2 - View Course Status Reflecting SCORM Standards (Priority: P1)

As a student interacts with their enrolled courses, the system displays the current status of each course using SCORM 1.2 standard lesson_status values. The student sees statuses such as "Not Started," "In Progress," "Completed," "Passed," or "Failed" on their enrolled courses list and course detail pages.

**Why this priority**: Status visibility is the core ask of this feature. Students need to understand where they stand with each course at a glance. This works independently once a course has been launched and session data exists.

**Independent Test**: Can be tested by setting various lesson_status values (via SCORM API or test seed data) and verifying the enrolled courses list displays the correct human-readable status for each.

**Acceptance Scenarios**:

1. **Given** a student's course attempt has `cmi.core.lesson_status` set to "not attempted," **When** they view their enrolled courses, **Then** the course displays as "Not Started"
2. **Given** a student's course attempt has `cmi.core.lesson_status` set to "incomplete," **When** they view their enrolled courses, **Then** the course displays as "In Progress"
3. **Given** a student's course attempt has `cmi.core.lesson_status` set to "completed," **When** they view their enrolled courses, **Then** the course displays as "Completed"
4. **Given** a student's course attempt has `cmi.core.lesson_status` set to "passed," **When** they view their enrolled courses, **Then** the course displays as "Passed"
5. **Given** a student's course attempt has `cmi.core.lesson_status` set to "failed," **When** they view their enrolled courses, **Then** the course displays as "Failed"
6. **Given** a student's course attempt has `cmi.core.lesson_status` set to "browsed," **When** they view their enrolled courses, **Then** the course displays as "Browsed"

---

### User Story 3 - View Percentage Completion (Priority: P2)

The system calculates and displays a percentage completion value for each enrolled course based on the SCORM 1.2 `cmi.core.score.raw` field (range 0–100). Students see this percentage alongside the status on their enrolled courses list and course detail pages. If no score has been set, the system shows 0% or "N/A" rather than a misleading value.

**Why this priority**: Percentage completion gives students a quantitative measure of progress beyond the categorical status. It is explicitly requested and is directly supported by SCORM 1.2's score mechanism.

**Independent Test**: Can be tested by setting various `cmi.core.score.raw` values during or after a SCORM session and verifying the enrolled courses view displays the correct percentage.

**Acceptance Scenarios**:

1. **Given** a student's course attempt has `cmi.core.score.raw` set to 75, **When** they view their enrolled courses, **Then** the course displays "75%" completion
2. **Given** a student's course attempt has no score set (score.raw is null or empty), **When** they view their enrolled courses, **Then** the course displays "0%" or "N/A" for completion
3. **Given** a student's course attempt has `cmi.core.score.raw` set to 100 and `lesson_status` set to "passed," **When** they view their enrolled courses, **Then** the course displays "100%" completion with a "Passed" status
4. **Given** a student's course attempt has `cmi.core.score.raw` set to 0 and `lesson_status` set to "failed," **When** they view their enrolled courses, **Then** the course displays "0%" completion with a "Failed" status

---

### User Story 4 - Status Updates During and After a Session (Priority: P2)

As a student works through a SCORM course, their status updates in real-time based on the course content's `LMSSetValue` calls. When the session commits or finishes, the final status and score are persisted. If a course content marks the student as "failed" (e.g., score below a threshold), the status reflects that immediately.

**Why this priority**: This bridges the launch and status features — status must be kept current as students interact. It depends on the SCORM runtime being functional but is a distinct concern focused on status transitions.

**Independent Test**: Can be tested by launching a course, observing status change to "In Progress," then simulating content that sets `lesson_status` to "failed" and verifying the persisted status updates correctly.

**Acceptance Scenarios**:

1. **Given** a student has launched a course, **When** the SCORM content calls `LMSSetValue("cmi.core.lesson_status", "incomplete")`, **Then** the enrollment view reflects "In Progress" status
2. **Given** a student is in an active session, **When** the SCORM content sets `lesson_status` to "failed" and calls `LMSCommit`, **Then** the course status updates to "Failed" and is visible after the session ends
3. **Given** a student completes a course with `lesson_status` set to "completed" and `score.raw` of 92, **When** the session finishes via `LMSFinish`, **Then** the course shows "Completed" status with "92%" completion on the enrolled courses list
4. **Given** a student abandons a session without committing (e.g., browser crash), **When** they view their enrolled courses, **Then** the course retains the last committed status (not the in-memory state that was lost)

---

### Edge Cases

- **Status transitions**: SCORM 1.2 defines valid transitions between lesson_status values. If content attempts an invalid transition (e.g., from "not attempted" directly to "passed" without "incomplete"), the system accepts the value but may log a warning. The system does not enforce transition rules — it records what the content reports.
- **Score out of range**: If `score.raw` is set outside 0–100, the system rejects the value per SCORM 1.2 and returns an error code. The displayed percentage is unaffected.
- **Score.scaled vs score.raw**: SCORM 1.2 defines both `score.raw` (0–100) and `score.scaled` (0.00–1.00). The system uses `score.raw` for percentage display. If only `score.scaled` is set, the system derives the percentage as `score.scaled * 100` (rounded to nearest integer).
- **Multiple attempts**: When a student retries a failed or incomplete course, the most recent attempt's status and score are displayed. Prior attempts are retained in history but the primary display shows the latest outcome.
- **Session loss before commit**: If Valkey is flushed or the server restarts during an active session, in-memory SCORM state is lost. The student's status reverts to the last committed checkpoint.
- **Browse-only content**: Some SCORM content may only set `lesson_status` to "browsed" (indicating the student viewed content without measurable objectives). The system displays this status without computing a percentage (shows 0% or N/A).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow an enrolled student to launch a SCORM course, transitioning the course status from "Not Started" to "In Progress" upon session initialization
- **FR-002**: System MUST display the SCORM 1.2 standard lesson_status values on the enrolled courses view using these mappings: "not attempted" → "Not Started", "incomplete" → "In Progress", "completed" → "Completed", "passed" → "Passed", "failed" → "Failed", "browsed" → "Browsed"
- **FR-003**: System MUST display percentage completion for each enrolled course, derived from `cmi.core.score.raw` (0–100 range) or `cmi.core.score.scaled` (0.00–1.00 range, multiplied by 100)
- **FR-004**: System MUST show "0%" or "N/A" for completion percentage when no score has been set by the SCORM content
- **FR-005**: System MUST update the displayed status and percentage in real-time as the SCORM content calls `LMSSetValue` during an active session
- **FR-006**: System MUST persist the final status and score on `LMSCommit()` or `LMSFinish()` so they survive session termination and application restarts
- **FR-007**: System MUST prevent launching a course without a valid enrollment
- **FR-008**: System MUST prevent duplicate concurrent sessions for the same course — reject with a clear message
- **FR-009**: System MUST display the most recent attempt's status and score as the primary status on the enrolled courses view
- **FR-010**: System MUST validate `cmi.core.score.raw` to be within 0–100 and reject out-of-range values per SCORM 1.2 specification
- **FR-011**: System MUST handle the "browsed" status appropriately — display the status but show 0% or N/A for completion (since browsed content has no measurable score)
- **FR-012**: System MUST display status and completion information consistently across the enrolled courses list view and the individual course detail page

### Key Entities

- **CourseAttempt**: Represents a single student's attempt at a SCORM course. Contains student ID, course ID, `lesson_status` (mapped to SCORM 1.2 values: not attempted, incomplete, completed, passed, failed, browsed), `score.raw` (0–100), `score.scaled` (0.00–1.00), session duration, and timestamps (started, committed, finished). Multiple attempts per student/course are supported.
- **EnrollmentStatus**: The computed display state for an enrollment on the student-facing UI. Derived from the most recent `CourseAttempt` and includes: human-readable status label ("Not Started", "In Progress", "Completed", "Passed", "Failed", "Browsed"), percentage completion (0–100 or N/A), and the attempt count.
- **ScormSession**: Ephemeral in-progress state for an active SCORM session. Holds the live `cmi.*` key/value bag during the session (including `lesson_status`, `score.raw`, `session_time`, `suspend_data`). Written to a `CourseAttempt` on `LMSCommit`/`LMSFinish`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An enrolled student can launch a SCORM course and see the status update to "In Progress" within 3 seconds of clicking Launch
- **SC-002**: All six SCORM 1.2 lesson_status values (not attempted, incomplete, completed, passed, failed, browsed) are displayed correctly with their human-readable labels on the enrolled courses view
- **SC-003**: Percentage completion is displayed accurately for any `score.raw` value in the 0–100 range, with correct rounding to the nearest integer
- **SC-004**: Status and percentage are consistent across the enrolled courses list and the individual course detail page with no discrepancy
- **SC-005**: After a session ends (via `LMSFinish` or `LMSCommit`), the final status and score are visible on the enrolled courses page within 1 second
- **SC-006**: Students with no score set see "0%" or "N/A" rather than a missing or broken display element
- **SC-007**: Out-of-range score values (negative or above 100) are rejected without causing display errors or corrupting the completion percentage

## Assumptions

- **SCORM 1.2 simplified**: The system supports SCORM 1.2 as defined in the project constitution. SCORM 2004 sequencing, `cmi.interactions`, and advanced status tracking are out of scope.
- **Existing SCORM runtime**: The SCORM runtime API (`LMSInitialize`, `LMSFinish`, `LMSGetValue`, `LMSSetValue`, `LMSCommit`) is already functional from spec 002. This spec focuses on status display and percentage completion on top of the existing runtime.
- **Existing enrollment**: Students are already enrolled in courses via the enrollment flow from spec 001. This spec assumes valid enrollments exist.
- **Status display is read-only for students**: Students cannot manually change their status or score. Status changes only occur through SCORM content API calls or system events.
- **Valkey for session state, MSSQL for persistence**: Active session state (including current `lesson_status` and `score.raw`) lives in Valkey during the session and is persisted to MSSQL on commit/finish, per the project's storage architecture.
- **Single score per attempt**: The percentage completion is derived from a single score value per attempt. Aggregating scores across multiple attempts or objectives is out of scope.
- **Razor Pages web portal**: The status and percentage display is rendered in the existing Razor Pages UI, consistent with prior slices.
