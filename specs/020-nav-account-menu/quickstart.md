# Quickstart Validation: Navigation Bar and Account Menu

**Feature**: 020-nav-account-menu
**Date**: 2025-01-31

## Prerequisites

1. Application is running (`dotnet run --project src/Host`)
2. MSSQL and Valkey are running (`docker compose up -d`)
3. Database is seeded with test data (existing seeder)

## Validation Steps

### 1. Nav Bar Colors and Layout

**Action**: Open any authenticated page (e.g., `/MyCourses/Index`) at 1280px viewport.

**Expected**:
- Nav background is #201e1d (dark warm neutral, not orange)
- Brand wordmark "Libre LMS" in Caprasimo serif at 20px, color #f5ead8
- Nav links (My Courses, Browse Courses) at 14px/weight 600, color #c9c2ba
- Page background is #f5ead8

### 2. Account Control — Desktop

**Action**: Look at the right side of the nav bar.

**Expected**:
- Role toggle pill visible with Learner and Admin buttons
- Active role has #c67139 background with white text
- Inactive role has transparent background with #c9c2ba text
- User name displayed (e.g., "Alice Johnson") at 13px/weight 600, color #f5ead8
- Chevron-down icon at 14px, color #c9c2ba
- Hover on account control shows #3a3634 background

### 3. Role Toggle Behavior

**Action**: Click the Admin button in the role pill.

**Expected**:
- Admin button becomes highlighted (#c67139 bg, white text)
- Learner button becomes unhighlighted
- Dashboard link appears in the nav (if admin view)
- User name does NOT change
- Account dropdown does NOT open

**Action**: Click the Learner button.

**Expected**:
- Learner button becomes highlighted
- Dashboard link disappears
- User name still unchanged

### 4. Account Dropdown

**Action**: Click the account control (on the name or chevron, not the role pill).

**Expected**:
- Dropdown appears below the account control
- Dropdown has white card background, rounded corners, subtle shadow
- Two rows: "View Profile" (with user icon) and "Settings" (with settings icon)
- No Logout row in the dropdown
- Hover on dropdown rows shows #e4d9c8 background

**Action**: Click outside the dropdown.

**Expected**: Dropdown closes.

**Action**: Click "View Profile".

**Expected**: Navigates to `/Account/Profile`.

**Action**: Click "Settings".

**Expected**: Navigates to `/Account/Settings`.

### 5. Active Link Highlighting

**Action**: Navigate to My Courses page.

**Expected**: My Courses link has #c67139 color (active state).

**Action**: Navigate to Browse Courses page.

**Expected**: Browse Courses link has #c67139 color; My Courses reverts to #c9c2ba.

### 6. Hover States

**Action**: Hover over each nav link.

**Expected**: Text color changes to #ffffff on hover (when not active).

**Action**: Hover over the hamburger button (mobile).

**Expected**: Background changes to #3a3634.

### 7. Mobile Layout (≤760px)

**Action**: Resize browser to 760px or less.

**Expected**:
- Nav links are hidden
- Hamburger icon button (20px, stroke-width 2.75) is visible
- Account name text is hidden in the collapsed bar
- Role pill and chevron remain visible in the account area
- Page heading is 28px (not 36px)
- Page padding is reduced
- Toolbars/hero rows stack vertically

**Action**: Click the hamburger button.

**Expected**:
- Dropdown opens with role toggle pill (centered) and page links as full-width rows
- Active link has #f3ddc9 background and #8a4f26 text
- Links are tappable and navigate correctly

### 8. Settings Page Logout

**Action**: Navigate to Settings page via the account dropdown.

**Expected**:
- Email notifications toggle is visible
- Theme selector is visible
- Logout is the last row (in a separate card below the form)
- No Logout appears anywhere in the top nav

### 9. Edge Cases

**Action**: Click the role toggle while the dropdown is open.

**Expected**: Role changes but dropdown stays open (stopPropagation).

**Action**: Click the same role button that is already active.

**Expected**: No visual change (already active).

**Action**: Resize from desktop (1280px) to mobile (375px) and back.

**Expected**: Layout reflows correctly without errors or state loss.

## CSS Audit

Run this to verify no hardcoded hex values in nav CSS:

```bash
grep -n '#[0-9a-fA-F]\{6\}' src/Host/wwwroot/css/site.css | grep -v ':root' | grep -v '//'
```

All hex values in nav selectors should be references to CSS custom properties, not raw hex.

## Architecture Tests

```bash
dotnet test tests/ArchitectureTests
```

Must pass — no module boundary changes in this feature.
