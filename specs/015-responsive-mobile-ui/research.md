# Research: Responsive Mobile UI — Phase 0

## Decisions

### Decision 1: CSS Organization — Single stylesheet, no framework

**Decision**: Extract all inline styles into `wwwroot/css/site.css` with CSS custom properties for design tokens and media queries for breakpoints. No CSS framework (Bootstrap, Tailwind, etc.).

**Rationale**:
- The Constitution (Principle II) forbids adding frameworks "unless a specific, current problem requires it." Responsive CSS is not such a problem — plain CSS media queries handle it fully.
- Adding Bootstrap or Tailwind would require a build pipeline (npm, bundling) that doesn't exist in this project, increasing operational complexity.
- The current codebase uses plain inline `<style>` and `style=""`. A single stylesheet is the minimal change.

**Alternatives considered**:
- **Bootstrap 5**: Would provide grid, nav component, cards — but requires npm, bundling, and overrides Razor Pages conventions. Too much for a responsive enhancement.
- **Tailwind CSS**: Utility-first approach would eliminate all custom CSS — but requires PostCSS build step. Overkill for ~30 pages.
- **CSS Modules / SCSS**: Requires build tooling not present in the project.

### Decision 2: Breakpoint Strategy

**Decision**: Three breakpoints — 480px (mobile), 768px (tablet), 1024px (desktop). Mobile-first media queries.

**Rationale**:
- 480px covers iPhone SE and most phones in portrait
- 768px covers iPads and tablets in portrait
- 1024px is the boundary where desktop layout is guaranteed
- Mobile-first (base styles for mobile, `min-width` queries for larger screens) is the recommended approach and results in less CSS for the most common case

**Alternatives considered**:
- Two breakpoints (mobile/desktop only): Would miss the tablet sweet spot where the admin dashboard needs a 2-column layout
- More granular breakpoints: Unnecessary for this codebase's current layout complexity

### Decision 3: Navigation Pattern — Hamburger menu on mobile

**Decision**: Convert the `<nav>` to a hamburger toggle on viewports ≤ 480px, using a minimal vanilla JS toggle (no framework).

**Rationale**:
- The current navbar has 8+ links on admin pages — impossible to fit on a 375px viewport
- A hamburger menu is the universally expected mobile pattern
- Vanilla JS avoids adding a dependency; the toggle is ~10 lines of code
- The existing HTMX script tag in `_Layout.cshtml` shows that vanilla JS is already acceptable in this project

**Alternatives considered**:
- **Bottom navigation bar**: Common in native mobile apps but unusual for web admin portals. Would require significant layout restructuring.
- **Horizontal scrollable nav**: Simpler to implement but poor UX — users don't expect to swipe to find menu items.
- **Accordion/collapsible groups**: Overly complex for a flat nav structure.

### Decision 4: Data Tables on Mobile — Horizontal scroll wrapper

**Decision**: Wrap admin data tables in a scrollable container (`overflow-x: auto`) on mobile/tablet, keeping the page chrome (filters, buttons) outside the scroll area.

**Rationale**:
- Tables with 5+ columns (Name, Email, Role, Organization, Actions) cannot be meaningfully stacked vertically
- Horizontal scrolling is the standard pattern for data tables on mobile
- Users on mobile are less likely to need full table visibility — the primary mobile audience is students browsing courses

**Alternatives considered**:
- **Card-based table replacement**: Each row becomes a card on mobile. This is more work and the admin pages are lower priority (P2).
- **Column hiding**: Hide less-important columns on mobile. Loses information and requires JS to toggle visibility.

### Decision 5: Desktop Parity — Preserve current look at ≥ 1024px

**Decision**: Desktop layout (≥ 1024px) must be visually identical to the current design. Mobile-first CSS means base styles target mobile, and `min-width: 1024px` queries restore the desktop layout.

**Rationale**:
- The spec (FR-012) requires desktop parity
- This is an enhancement, not a redesign
- Testing: visually compare before/after screenshots at 1280px

## No NEEDS CLARIFICATION Items

All technical decisions have been resolved. No further research needed before Phase 1 design.
