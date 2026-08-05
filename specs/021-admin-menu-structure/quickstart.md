# Quickstart: Admin Menu Structure Validation

## Prerequisites

- .NET 10 SDK installed
- Docker running (for MSSQL and Valkey)
- Database seeded with admin user accounts

## Setup

```bash
cd /workspace
docker compose up -d
dotnet restore
dotnet build
```

## Run the Application

```bash
cd src/Host
dotnet run --launch-profile Host
```

The app starts on the configured port (check `launchSettings.json`, typically `https://localhost:<port>`).

## Validation Scenarios

### Scenario 1: Admin sees all menu entries

1. Navigate to the app and log in as a user with `SuperUser` or `OrgAdmin` role
2. Switch the role toggle to **"Admin"**
3. Verify the navbar shows: My Courses, Browse Courses, Dashboard, Courses, Enrollments, Learners, Organizations, Upload
4. Click each admin link and confirm it navigates to the correct admin page

### Scenario 2: Learner mode hides admin entries

1. While logged in as admin, switch the role toggle to **"Learner"**
2. Verify only My Courses and Browse Courses are visible
3. Switch back to **"Admin"** and verify admin links reappear

### Scenario 3: Pure learner sees no admin entries

1. Log in as a user with only the `Learner` role
2. Verify only My Courses and Browse Courses are visible
3. Verify no role toggle pill appears

### Scenario 4: Mobile viewport

1. Resize browser to ≤760px width
2. Open the hamburger menu
3. Verify admin links appear/disappear with the role toggle (same as desktop)

### Scenario 5: Active link highlighting

1. Navigate to each admin page directly (e.g., `/Admin/Courses/Index`)
2. Verify the corresponding nav link has the `.active` visual state
