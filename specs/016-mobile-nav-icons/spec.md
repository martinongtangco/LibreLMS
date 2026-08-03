# Bug Specification: Mobile Navigation Missing Icons

**Created**: 2025-08-03

**Status**: Draft

**Input**: "add icons when its in mobile view — all of the above (hamburger toggle, nav item icons, icon-only mobile nav)"

## Problem

The mobile navigation (hamburger menu) at ≤ 480px has no icons. Users get no visual affordance for:
1. Hamburger toggle state (open vs closed) — both states show the same `☰`
2. Menu item identification — all items are plain text links
3. Quick scanning — no icon-based navigation for fast recognition

## Acceptance Criteria

1. **Hamburger toggle**: Show `☰` when closed, `✕` when open
2. **Nav item icons**: Add an icon before each menu link text
3. **Icons are CSS-only**: No new JS libraries (Font Awesome, etc.) — use Unicode or inline SVG

## Icon Mapping

| Link | Icon |
|------|------|
| Browse Courses | 📚 |
| My Courses | 🎓 |
| Dashboard | 📊 |
| Organizations | 🏢 |
| Org Chart | 🌳 |
| Learners | 👥 |
| Courses | 📋 |
| Enrollments | ✍️ |
| Create Course | ➕ |
| Upload SCORM | 📤 |
| Login | 🔑 |
| Logout | 🚪 |

## Constraints

- No new NuGet packages or JS libraries
- Unicode emoji icons (already supported in all modern mobile browsers)
- Desktop view (≥ 1024px) must remain visually unchanged — icons only show in mobile nav
- Must not break HTMX behavior
