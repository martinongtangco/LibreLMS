# Quickstart Validation: Responsive Mobile UI

## Prerequisites

- Application running (see running instructions below)
- A modern browser with DevTools (Chrome, Firefox, Edge)

## Start the Application

```bash
cd /workspace/src/Host
ConnectionStrings__Sql="Server=mssql,1433;Database=LibreLms;User Id=sa;Password=Lms#vZdV361xAfdYmoEZmTmh!9;TrustServerCertificate=true;" \
ConnectionStrings__Valkey="valkey:6379" \
dotnet run --urls="http://0.0.0.0:5000"
```

Wait for "Now listening on: http://0.0.0.0:5000" in the console output.

## Validation Steps

### 1. Mobile Viewport (375px — iPhone SE baseline)

1. Open browser DevTools (F12 / Cmd+Opt+I)
2. Toggle device toolbar (Ctrl+Shift+M)
3. Select "iPhone SE" preset (375 × 667) or manually set width to 375px
4. Navigate to each page and verify:

| Page | URL | Check |
|------|-----|-------|
| Browse Courses | `/Courses` | Single-column cards, no horizontal scroll, hamburger nav opens/closes |
| Course Detail | `/Courses/{guid}` | Stacked layout, enroll button ≥ 44px tall |
| My Courses | `/MyCourses` | Enrollment rows readable, no horizontal scroll |
| Login | `/Account/Login` | Form centered, inputs full-width, button full-width |
| Dashboard | `/Admin/Dashboard/Index` | Metric cards stack vertically, no overflow |
| Learner List | `/Admin/Learners/Index` | Table scrolls horizontally, filters wrap |
| Org List | `/Admin/Organizations/Index` | Org items readable, buttons visible |

### 2. Tablet Viewport (768px — iPad portrait)

1. Set viewport width to 768px
2. Verify:

| Page | Check |
|------|-------|
| Dashboard | Metric cards in 2-column grid |
| Learner List | Table fits or scrolls horizontally without page overflow |
| Browse Courses | Cards may show 2-column grid |
| Navigation | Hamburger or full nav — no overflow |

### 3. Desktop Viewport (1280px — full desktop)

1. Set viewport width to 1280px (or uncheck device toolbar)
2. Verify layout is **visually identical** to the pre-change design:
   - Navbar shows all links horizontally
   - Course cards in existing layout
   - Container max-width 960px centered
   - All badges, buttons, forms appear unchanged

### 4. Touch Target Check

1. In DevTools, enable "Emulate touch" (in device toolbar settings)
2. On mobile viewport (375px), try tapping every button, link, and form input
3. Verify no tap target is smaller than 44×44 pixels

### 5. Horizontal Scroll Check

1. On 375px viewport, scroll each page horizontally
2. The page content must **not** scroll horizontally (except data tables wrapped in scroll containers)
3. If horizontal scroll occurs on non-table content, it's a defect

## Pass/Fail Criteria

| Criterion | Pass Condition |
|-----------|---------------|
| No horizontal scroll | Page does not scroll horizontally at 375px (except wrapped tables) |
| Touch targets | All buttons, links, inputs are ≥ 44×44px on mobile |
| Nav accessible | Hamburger menu opens/closes and all links are reachable |
| Desktop parity | Layout at 1280px is visually identical to pre-change design |
| Core journey | Browse → Detail → Enroll completes without usability blockers |

## Demo Credentials

- **Student**: `alice@example.com` / `password123`
- **Admin**: `admin@example.com` / `password123`
