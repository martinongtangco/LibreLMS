# UI Contract: Admin Organizations Tree

**Feature**: `specs/036-org-tree-branching` | **Date**: 2026-08-24
**Surface**: `GET /Admin/Organizations/Index` (Razor page, server-rendered)

This is a **UI contract** — the observable DOM/behavior contract the page must satisfy. It is what
the Playwright suite asserts against; it deliberately describes structure and classes, not CSS
pixel values.

## 1. Page contract (unchanged behavior)

- Auth: `SuperUser` or `OrgAdmin` (existing `[Authorize]`), page title "Organizations".
- Page-level entry points remain present and functional: **Org Chart View** link (to the spec-013
  chart) and **Create Organization** link.
- Error state: a load failure shows the existing danger alert text; no tree region is rendered.

## 2. Tree region structure

```html
<ul class="org-tree" aria-label="Organization hierarchy">
  <li class="org-node org-node--root">
    <div class="org-node__card">
      <span class="org-node__name">Root Organization</span>
      <span class="org-node__description">…</span>            <!-- only when Description non-empty -->
      <span class="badge org-node__root-badge">Root</span>     <!-- root nodes only -->
      <span class="badge org-node__disabled-badge">Disabled</span> <!-- disabled nodes only -->
      <div class="org-node__actions">
        <a class="btn btn-secondary" href="/Admin/Organizations/Edit?id={id}">Edit</a>
      </div>
    </div>
    <ul class="org-tree org-tree--nested">                    <!-- only when children exist -->
      <li class="org-node"> …one <li> per child, same shape… </li>
    </ul>
  </li>
</ul>
```

**Rules** (each maps to a spec FR):

| Rule | Contract requirement | Spec ref |
|------|---------------------|----------|
| C-01 | Exactly one top-level `<li>` exists (the root, `org-node--root`); every other organization appears as exactly one `<li class="org-node">` somewhere in the tree | FR-001, FR-005 |
| C-02 | A node's depth equals its `<ul class="org-tree">` nesting depth (root = 1st level). Indentation is purely structural (nested lists), never inline offsets | FR-002, FR-004 |
| C-03 | Every non-root `<li>` is inside its parent node's nested `<ul>` — the DOM nesting itself is the parent/child trace | FR-003 |
| C-04 | Siblings share the same parent `<ul>` and therefore the same indentation level | FR-004 |
| C-05 | Leaf nodes (no children) contain **no** nested `<ul>` — no phantom structure | edge case |
| C-06 | Connector lines: every non-root `<li>` renders a visible connector (vertical line + horizontal elbow to the parent spine) drawn by CSS on the list/li elements; lines use border tokens (`--color-border` / `--color-border-strong`); no JS, no images | FR-003 |
| C-07 | Root row carries `org-node--root`, the `Root` badge, and visibly stronger card styling than non-root rows | FR-005 |
| C-08 | A disabled node (own or ancestor-disabled) carries `org-node--disabled` + `Disabled` badge + muted styling; node remains visible in place | FR-007 |
| C-09 | Every node card shows the name; the description span only when a description exists; the Edit action links to the existing edit route for that node's `Id` | FR-006 |
| C-10 | All colors, fonts, radii, spacing derive from existing `:root` design tokens in `site.css` (Organic system) | FR-008 |
| C-11 | At viewport widths ≥ 375px the tree container has no horizontal overflow (`scrollWidth <= clientWidth`); per-level indent step may shrink at existing breakpoints but hierarchy stays discernible | FR-009, FR-010 |
| C-12 | Organization names are rendered as HTML-encoded text (no markup injection) | edge case |

## 3. Behavior contract

- **B-01**: Tree is fully expanded on load; no collapse/expand controls exist.
- **B-02**: No JavaScript executes to build or modify the tree (server-rendered, static after load).
- **B-03**: Creating a new organization via the existing Create flow then reloading shows the new
  node at the correct nesting position.
- **B-04**: Sibling order is creation order (existing service ordering), stable across reloads.

## 4. E2E assertion map (for the Playwright suite)

| Contract | Playwright check (sketch) |
|----------|---------------------------|
| C-01 | `page.locator('ul.org-tree > li')` count is 1; total `.org-node` count equals org count |
| C-02/C-03 | Billing's `<li>` is a descendant of Finance's `<li>` and not of Sales' |
| C-04 | Finance and Sales share the same parent `<ul>` |
| C-06 | Computed border/line elements present on non-root `<li>`s; none on root |
| C-07 | Root `<li>` has `org-node--root` class and visible `Root` badge |
| C-08 | Disabled node + all descendants have `org-node--disabled` |
| C-11 | `evaluate(scrollWidth <= clientWidth)` on the tree container at 375px |
