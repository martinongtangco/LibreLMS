# Quickstart: SCORM Launch & Completion

**Branch**: `002-scorm-launch-completion`

## Prerequisites

- MSSQL and Valkey containers running (`docker compose up -d`)
- Slice 1 (Course Catalog + Enrollment) implemented and working
- Test students and courses seeded (from Slice 1 seeders)

## Validation Scenarios

### 1. Upload a SCORM Package

1. Navigate to the admin upload page (or use API directly)
2. Upload a valid SCORM 1.2 ZIP package containing `imsmanifest.xml`
3. **Expected**: Package appears in the catalog as a launchable SCORM course

**API Test**:
```bash
curl -X POST http://localhost:5000/api/scorm/upload \
  -F "package=@/path/to/scorm-package.zip" \
  -H "Authorization: Bearer <admin-token>"
```
Expected: `201 Created` with `packageId`, `courseId`, `title`

---

### 2. Launch a SCORM Course

1. Log in as a test student enrolled in a SCORM course
2. Navigate to the course detail page
3. Click "Launch"
4. **Expected**: SCORM player page loads showing course content within 3 seconds

**API Test**:
```bash
curl -X POST http://localhost:5000/api/scorm/<courseId>/launch \
  -H "Authorization: Bearer <student-token>"
```
Expected: `200 OK` with `sessionId`, `contentUrl`, `apiUrl`

---

### 3. Track Progress During Session

1. With a SCORM session active, simulate `LMSSetValue` calls
2. **Expected**: Values are stored and retrievable via `LMSGetValue`

**API Test**:
```bash
# Set a value
curl -X POST http://localhost:5000/api/scorm/session/<sessionId>/setValue \
  -H "Content-Type: application/json" \
  -d '{"element":"cmi.core.lesson_status","value":"incomplete"}'

# Get the value back
curl "http://localhost:5000/api/scorm/session/<sessionId>/getValue?element=cmi.core.lesson_status"
```
Expected: `200 OK` with `{"value":"incomplete"}`

---

### 4. Commit and Complete a Session

1. Set lesson_status to "completed" and a score
2. Call `LMSFinish`
3. **Expected**: Completion record saved to MSSQL

**API Test**:
```bash
# Set status and score
curl -X POST http://localhost:5000/api/scorm/session/<sessionId>/setValue \
  -H "Content-Type: application/json" \
  -d '{"element":"cmi.core.lesson_status","value":"completed"}'

curl -X POST http://localhost:5000/api/scorm/session/<sessionId>/setValue \
  -H "Content-Type: application/json" \
  -d '{"element":"cmi.core.score.raw","value":"85"}'

# Finish session
curl -X POST http://localhost:5000/api/scorm/session/<sessionId>/finish \
  -H "Content-Type: application/json" \
  -d '{"exit":"normal"}'
```
Expected: `200 OK` with `{"success":true,"status":"completed","score":85}`

---

### 5. View Completion Results

1. Navigate to "My Courses" page after completing a session
2. **Expected**: Course shows as "Completed" with score and completion date

**API Test**:
```bash
curl http://localhost:5000/api/scorm/attempts/my \
  -H "Authorization: Bearer <student-token>"
```
Expected: `200 OK` with attempts array showing `status: "completed"` and `scoreRaw: 85`

---

### 6. Resume from Checkpoint

1. Start a session, set `cmi.suspend_data` with a bookmark
2. Call `LMSCommit` (not `LMSFinish`) — session data committed but not finished
3. End the session (close tab or call `LMSFinish` with status "incomplete")
4. Relaunch the same course
5. **Expected**: `LMSGetValue("cmi.suspend_data")` returns the previously saved bookmark

---

### 7. Reject Concurrent Sessions

1. Launch a SCORM course (session active)
2. Attempt to launch the same course again in a second tab
3. **Expected**: "Session already active" error message

---

### 8. Reject Invalid Score Values

1. With a session active, attempt to set `cmi.core.score.raw` to `105` or `-10`
2. **Expected**: `LMSSetValue` returns `false`, error code set

**API Test**:
```bash
curl -X POST http://localhost:5000/api/scorm/session/<sessionId>/setValue \
  -H "Content-Type: application/json" \
  -d '{"element":"cmi.core.score.raw","value":"105"}'
```
Expected: `400 Bad Request` with `success: false` and an error code

---

## Rollback

To reset SCORM data for testing:
```bash
# Clear Valkey session data
docker exec valkey redis-cli FLUSHDB

# Delete SCORM tables from MSSQL (keeps courses/enrollments intact)
docker exec mssql /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" \
  -d "master" \
  -Q "USE LearningLms; DROP TABLE IF EXISTS CourseAttempts; DROP TABLE IF EXISTS ScormPackages;"
```
