# Quickstart: Organic Design System Redesign

**Feature**: 017-organic-ui-redesign
**Date**: 2026-08-03

## Prerequisites

- Devcontainer running (`docker compose up` brings up `mssql` + `valkey`, per constitution).
- `dotnet run --project src/Host` builds and serves the app.
- Seeded demo data available (existing `ManagementSeeder`/enrollment seed data) — at least one learner with an enrolled course and one admin account (`SuperUser` or `OrgAdmin`).

## Setup

```bash
dotnet build
dotnet ef database update --project src/Host   # applies the new Student columns migration
dotnet run --project src/Host
```

## Validation Scenarios

Each scenario maps to an acceptance scenario in [spec.md](spec.md).

### 1. My Courses — visual + status/progress (US1)

1. Log in as a learner with at least one enrolled course.
2. Navigate to `/MyCourses`.
3. **Expect**: Organic-styled card(s) — cream background, rounded card, terracotta accents, category kicker, status tag, hours tag, pill progress bar, "Enrolled {date} · {pct}% complete" line. See [data-model.md](data-model.md) for the status/progress mapping.
4. Log in as a learner with zero enrollments. **Expect**: centered empty-state card with "You haven't enrolled in any courses yet." and a primary button to Browse Courses.

### 2. Browse Courses — search/filter/enroll (US1)

1. Navigate to `/Courses`.
2. Type a partial course title into search. **Expect**: grid filters live, Organic card styling retained.
3. Pick a category from the dropdown. **Expect**: grid filters to that category.
4. Click "Clear". **Expect**: both filters reset.
5. Search for something that matches nothing. **Expect**: "No courses match your search." message.
6. Click "View Details" on an unenrolled course, then "Enroll now". **Expect**: navigates to Course Detail, then the CTA becomes a disabled "✓ Enrolled" button and the course's Browse Courses card now shows the "✓ Enrolled" tag.

### 3. Admin Dashboard — stat tiles + course table (US2)

1. Log in as `SuperUser` (or `OrgAdmin`).
2. Navigate to `/Admin/Dashboard`.
3. **Expect**: existing scoped metrics rendered as stat tiles (kicker + large accent number), and an "All Courses" table listing every course visible to this admin's scope with category, hours, and an enrollment count matching the actual number of `Enrollment` rows for that course (spot-check one course's count against `/Admin/Enrollments`).

### 4. Profile / Settings (US3)

1. Click the avatar/name control in the top nav. **Expect**: dropdown with exactly "View Profile" and "Settings" — no Logout here.
2. Click "View Profile". **Expect**: name, role, email in a bordered-row card.
3. Click back, open "Settings". Toggle email notifications off and change the theme selector, then reload `/Account/Settings`. **Expect**: both choices are still shown as set (persisted via `GetPreferencesAsync`/`UpdatePreferencesAsync`).
4. Click "Logout" (last row on Settings). **Expect**: signed out, same as today's logout behavior.

### 5. Mobile responsiveness (US4)

1. Resize the browser (or device emulator) to 375px width.
2. On any redesigned page: **Expect** the nav collapses behind a hamburger button; opening it shows the role-appropriate page links; the avatar/profile control remains visible outside the hamburger; headings render at the mobile size; toolbars/hero blocks stack vertically; no horizontal scrolling anywhere.

## Regression Check

Run the existing automated suite to confirm no functional regressions from the visual change (SC-005):

```bash
dotnet test tests/ArchitectureTests
dotnet test tests/Catalog.Tests
dotnet test tests/Enrollment.Tests
dotnet test tests/Scorm.Tests
```

All four projects must pass unchanged (plus new unit tests for `GetPreferencesAsync`, `UpdatePreferencesAsync`, and `GetEnrollmentCountsByCourseAsync` added to `Enrollment.Tests`).
