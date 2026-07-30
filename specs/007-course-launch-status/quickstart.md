# Quickstart: Course Launch & Status Tracking

**Date**: 2025-07-30
**Feature**: 007-course-launch-status

## Prerequisites

- .NET 10 SDK installed and active (verified via `dotnet --version`)
- Docker running (for MSSQL + Valkey services)
- Repository checked out on `story/007-course-launch-status` branch

## Setup

```bash
# Start infrastructure services
docker compose up -d mssql valkey

# Run migrations (if any new migrations are generated)
cd src/Host
dotnet ef database update --project ../../src/Host

# Seed data (courses, students, enrollments, SCORM packages)
dotnet run -- seed  # if a seed command exists, otherwise use existing seed data
```

## Validation Scenarios

### Scenario 1: View "Not Started" status for a new enrollment

1. **Arrange**: Enroll a student in a SCORM course but do not launch it
2. **Act**: Navigate to `/MyCourses` as the student
3. **Assert**: The course row shows a "Not Started" badge with no score/percentage displayed

### Scenario 2: View "In Progress" status during an active session

1. **Arrange**: Enroll a student in a SCORM course
2. **Act**: Navigate to `/Scorm/Launch/{courseId}` to start a session, then navigate back to `/MyCourses`
3. **Assert**: The course row shows an "In Progress" badge

### Scenario 3: View "Completed" status with percentage

1. **Arrange**: Complete a SCORM session where `lesson_status` is set to "completed" and `score.raw` is set to 85
2. **Act**: Navigate to `/MyCourses`
3. **Assert**: The course row shows a "Completed" badge with "85%" displayed

### Scenario 4: View "Passed" status with 100%

1. **Arrange**: Complete a SCORM session where `lesson_status` is set to "passed" and `score.raw` is set to 100
2. **Act**: Navigate to `/MyCourses`
3. **Assert**: The course row shows a "Passed" badge with "100%" displayed

### Scenario 5: View "Failed" status

1. **Arrange**: Complete a SCORM session where `lesson_status` is set to "failed" and `score.raw` is set to 45
2. **Act**: Navigate to `/MyCourses`
3. **Assert**: The course row shows a "Failed" badge with "45%" displayed

### Scenario 6: View "Browsed" status with N/A percentage

1. **Arrange**: Complete a SCORM session where `lesson_status` is set to "browsed" and no score is set
2. **Act**: Navigate to `/MyCourses`
3. **Assert**: The course row shows a "Browsed" badge with "N/A" for percentage

### Scenario 7: Score of exactly 0 is saved and displayed

1. **Arrange**: Complete a SCORM session where `score.raw` is set to 0
2. **Act**: Navigate to `/MyCourses`
3. **Assert**: The course row shows "0%" (not null/N/A)

### Scenario 8: Launch without enrollment shows error

1. **Act**: Attempt to navigate to `/Scorm/Launch/{courseId}` as a student who is NOT enrolled in that course
2. **Assert**: The launch page shows an error: "You are not enrolled in this course."

### Scenario 9: Duplicate session is rejected

1. **Arrange**: Start a SCORM session for a course
2. **Act**: Attempt to launch the same course again (e.g., in another tab)
3. **Assert**: The launch page shows an error: "A session for this course is already active."

## Run Tests

```bash
# Run all tests
dotnet test

# Run Scorm.Tests specifically
dotnet test tests/Scorm.Tests/

# Run architecture tests (module boundary checks)
dotnet test tests/ArchitectureTests/
```

## Build

```bash
dotnet build
```

## References

- [Data Model](./data-model.md) — entity definitions and field details
- [Status Mapping Contract](./contracts/status-mapping.md) — mapping table and display rules
- [Spec](./spec.md) — full feature specification with acceptance criteria
