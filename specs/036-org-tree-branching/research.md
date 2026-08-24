# Phase 0 Research: Organization Tree Branching

**Feature**: `specs/036-org-tree-branching` | **Date**: 2026-08-24

All Technical Context items were known from the codebase (no NEEDS CLARIFICATION markers existed).
This document resolves the design unknowns the plan surfaced.

## R1: Tree rendering approach — server-rendered nested list + pure CSS

- **Decision**: Render the tree as a semantic nested list (`<ul class="org-tree">` → `<li>` per
  organization, children in a nested `<ul>`), with all depth indentation and parent→child connector
  lines drawn by CSS (pseudo-elements + borders) on the existing markup. No JavaScript, no new
  client-side libraries.
- **Rationale**:
  - The page model already builds the full in-memory tree (`OrgTreeNode.Children`), so nested
    markup maps 1:1 to the data with zero server-side rework.
  - CSS-only connectors are the standard, minimal-mechanism solution for "tree list" UIs; they are
    deterministic (no layout JS), work with server-rendered HTML, and are trivially assertable in
    Playwright (DOM structure + computed styles), which Constitution Principle XIII requires.
  - Principle II (no abstraction without a current problem) and the project's minimal-client-JS
    norm (the only page-specific JS today is the zoom/pan chart, which is a different interaction)
    both point away from a JS tree component for a static, fully-expanded list.
- **Alternatives considered**:
  - *JS tree library (e.g., a client-side tree widget)*: rejected — adds a client dependency, a
    runtime rendering path the spec's fully-expanded static requirement doesn't need, and harder
    E2E assertion.
  - *Reuse the SVG chart approach (spec 013)*: rejected — the interactive chart is a separate,
    already-shipped surface (pan/zoom node boxes); a list/tree is the requested pattern for the
    management page and must stay a simple scannable document, not a canvas.
  - *Flat list with explicit "Parent: X" text per row*: rejected — the spec's core requirement is
    visual traceability ("branching and node visible to know who is the parent of what"); text
    labels don't satisfy it.

## R2: Connector line geometry — classic CSS tree elbows

- **Decision**: Draw the standard CSS-tree connectors: each `<li>` renders a vertical line segment
  above it and a horizontal elbow from the parent's spine to the node; the last child's vertical
  line terminates at the elbow. Lines are 1–2px, colored with the existing `--color-border` /
  `--color-border-strong` tokens.
- **Rationale**: The elbow pattern makes parent→child traceability unambiguous at any depth
  (SC-001/SC-002), which is exactly the reported defect (all nodes at one flat indent, no lines).
  Border-based lines stay crisp, scale with the indent, and need no images or SVG.
- **Alternatives considered**:
  - *Indent only, no lines*: rejected — the user explicitly asked for "tree like branching";
    indent alone was effectively the current (broken) state.
  - *SVG overlay per row*: rejected — same cost as the chart approach with none of its benefits for
    a list.

## R3: Root organization treatment

- **Decision**: The root row gets (a) an explicit "Root" badge (extending the existing `badge`
  pattern already used on this page) and (b) stronger card styling — brand-tinted border
  (`--color-brand` at reduced emphasis) and/or the category-tint background token — so it reads as
  the top of the tree even before tracing lines.
- **Rationale**: Spec FR-005 requires an explicit root indicator plus stronger styling; reusing the
  existing badge and token system keeps the change inside the Organic design system (FR-008).
- **Alternatives considered**: *Crown/tree icon glyph*: rejected as primary signal — decorative
  glyphs are weaker than text for accessibility and i18n; a "Root" text badge is unambiguous.

## R4: Disabled organization treatment

- **Decision**: A disabled organization and every descendant of a disabled organization render with
  muted styling (reduced opacity / `--color-text-faint` text) plus a "Disabled" badge. The node
  stays fully in-tree (no hiding, no re-parenting).
- **Rationale**: The domain contract (`Organization.IsDisabled` docs) already states "disabled orgs
  remain queryable but are visually distinct in the UI" and "org and all descendants are inactive" —
  the subtree-wide treatment follows directly from that invariant. Visibility is preserved per
  spec FR-007.
- **Alternatives considered**:
  - *Strike-through on the name*: rejected — reads as "deleted", conflicts with the soft-delete
    semantics (deleted orgs are hidden entirely).
  - *Collapsing the subtree*: rejected — breaks traceability (the feature's goal) and hides
    descendants the admin still needs to see.
- **Data note**: `IndexModel` must propagate disabled state down the subtree. `OrgTreeNode` (a
  page-level record in `Index.cshtml.cs`) is extended with an `IsDisabled` flag computed as
  "own flag OR any ancestor's flag" during `BuildTree`. `OrganizationService.ListAllAsync()`
  already returns full `Organization` entities (incl. `IsDisabled`) filtered by `!IsDeleted`, so no
  module/service change is needed.

## R5: Depth indentation, deep hierarchies, and mobile (FR-002, FR-009, FR-010)

- **Decision**: Indentation is produced by the natural nested-list structure (each `<ul>` level
  adds one fixed indent step defined by one CSS variable, e.g. `--org-tree-indent`). Per-level
  step is reduced at the project's existing breakpoints (mobile base ≤480px, ≤760px) so a 7-level
  tree stays on-screen at 375px with no horizontal scrolling. No JS scroll container is needed at
  the expected scale; if content ever exceeds the viewport height, the page scrolls normally.
- **Rationale**: CSS-variable-based indent makes the mobile compression a one-line media-query
  change and keeps the "no horizontal scroll at ≥375px" success criterion (SC-004) verifiable with
  a simple `scrollWidth <= clientWidth` Playwright assertion.
- **Alternatives considered**:
  - *Horizontal scroll region for deep trees*: rejected for v1 — the spec's success criterion is
    no horizontal scroll at all; compressing the indent step achieves readability within the
    existing container (`--container-max-width` layout).
  - *JS-computed depth class per node*: rejected — nesting already encodes depth in the DOM; extra
    classes and JS add nothing observable.

## R6: Accessibility semantics — native list, no ARIA tree

- **Decision**: Use native nested `<ul>`/`<li>` semantics (lists are already hierarchical to
  screen readers) plus an `aria-label` on the tree region. Do **not** add `role="tree"`/`role=
  "treeitem"`, which obligates keyboard navigation behavior that a static server-rendered page
  doesn't implement.
- **Rationale**: ARIA tree roles without JS keyboard support are an accessibility anti-pattern
  (they promise interaction the page doesn't deliver). Native lists are honest, zero-cost, and
  keep the markup simple per Principle IV.
- **Alternatives considered**: *Full ARIA tree with JS keyboard nav*: rejected — out of scope for a
  static management list; revisit only if collapse/keyboard interaction is ever added.

## R7: Sibling ordering

- **Decision**: Preserve the existing ordering from `OrganizationService.ListAllAsync()`
  (`OrderBy(o => CreatedAt)`). No new sort UI in this slice.
- **Rationale**: Changing or adding ordering is behavior beyond the reported defect; stable
  creation-order grouping is what admins see in the chart view today, and the spec is silent on
  reordering.
- **Alternatives considered**: *Alphabetical sibling sort*: rejected as a silent behavior change
  without spec backing.
