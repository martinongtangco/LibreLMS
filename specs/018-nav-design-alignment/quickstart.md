# Quickstart Validation: Nav & Header Design Alignment

**Feature**: 018-nav-design-alignment
**Date**: 2025-08-04

## Prerequisites

- Libre LMS running locally (docker compose + dotnet run)
- At least two test users: one learner, one admin (SuperUser or OrgAdmin)

## Validation Steps

### 1. Desktop Nav — No Emoji

1. Log in as any user
2. Verify all nav link icons are Lucide SVG icons (inspect with browser devtools — should see `<svg data-lucide="...">` elements, not HTML entities like `&#128218;`)
3. Verify zero emoji characters appear in the nav bar
4. Verify stroke-width on all SVG icons is 2.75

### 2. Profile Dropdown

1. Click the circular avatar in the top-right nav
2. Verify dropdown shows exactly two entries: "View Profile" and "Settings"
3. Verify neither entry links to Logout
4. Verify clicking outside closes the dropdown
5. Verify pressing Escape closes the dropdown

### 3. Role Switcher (Desktop)

1. As a learner, verify the nav shows Browse Courses and My Courses only
2. Click "Admin" on the role switcher
3. Verify admin links appear (Dashboard, Organizations, Org Chart, Learners, Courses, Enrollments, Create Course, Upload SCORM)
4. Click "Learner" again
5. Verify admin links disappear
6. Navigate to a different page — verify the role switcher state persists

### 4. Mobile Nav (≤760px)

1. Resize browser to 375px width
2. Verify only brand + hamburger + avatar are visible (no links, no role switcher, no name label)
3. Tap hamburger — verify dropdown shows role switcher + links
4. Tap outside dropdown — verify it closes
5. Verify user name label is hidden (avatar only)

### 5. CSS Token Audit

1. Open `src/Host/wwwroot/css/site.css`
2. Search for nav-related rules (outside `:root`)
3. Verify zero raw hex color values (no `#xxxxxx` patterns) in nav rules
4. Verify zero raw `px` values in nav rules (only `var(--...)` references)

### 6. Unauthenticated User

1. Log out
2. Verify nav shows only brand wordmark and Login link
3. Verify no role switcher, no avatar, no profile control

## Expected Outcome

All validation steps pass without errors. No visual regressions on existing pages (course cards, dashboard tiles, etc. retain their Organic styling).
