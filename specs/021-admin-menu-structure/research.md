# Research: Admin Menu Structure

## Decisions

### Decision 1: Navigation pattern

**Decision**: Add individual nav links alongside existing "My Courses" and "Browse Courses" links, all within the `.nav-links` container, each marked with `data-role-link="admin"` for toggle-controlled visibility.

**Rationale**: The existing `_Layout.cshtml` already uses `data-role-link="admin"` on the Dashboard link with CSS selectors (`[data-role-link="admin"]` defaults to hidden, `.role-admin` shows them). This pattern is established and works for both desktop and mobile. Extending it is the simplest approach.

**Alternatives considered**:
- Dropdown/mega-menu on "Admin" — would require new JS and CSS, breaking the current flat nav pattern
- Sidebar navigation on Dashboard page only — doesn't solve the discoverability problem (users must navigate to Dashboard first)
- Separate admin layout — over-engineered for 6 links

### Decision 2: Active link detection

**Decision**: Extend the existing JS `linkMap` object in `_Layout.cshtml` with entries for each admin page path.

**Rationale**: The current script already maps paths to `data-page` attributes for active-link highlighting. Adding 5 more entries is a 5-line change.

### Decision 3: Icons

**Decision**: Use Lucide icon names consistent with existing nav icons. Proposed mapping:
- Dashboard → `layout-dashboard` (already used)
- Courses → `book-open` (reuse) or `layers` — use `layers` to distinguish from Browse Courses
- Enrollments → `users`
- Learners → `user-round`
- Organizations → `building-2`
- Upload → `upload`

**Rationale**: Lucide is already loaded globally. These icon names match the semantic meaning of each section.

### Decision 4: Mobile behavior

**Decision**: Admin links appear in the same hamburger dropdown as learner links, just toggled by the existing CSS class mechanism.

**Rationale**: The mobile nav already shows the Dashboard link. The CSS media query at 760px already handles the hamburger dropdown. No new mobile-specific code needed.

## NEEDS CLARIFICATION Resolution

No clarifications were needed. The existing codebase provides all the patterns required:
- Role-based visibility: `[data-role-link="admin"]` + `.role-admin` body class
- Active link detection: JS `linkMap` object
- Mobile nav: existing hamburger dropdown at ≤760px
- Icon system: Lucide via `data-lucide` attribute
