# Research: Interactive Organization Chart View

**Feature**: 013-org-chart-view
**Date**: 2025-08-01

## Decision 1: Chart Rendering Approach

**Question**: How to render a zoomable, pannable org chart in the existing Razor Pages + HTMX stack without adding a JavaScript framework?

**Options evaluated**:

| Approach | Pros | Cons |
|----------|------|------|
| A. Custom SVG (server-rendered) | Full control, no new deps, native pan/zoom via `<svg viewBox>` | Complex layout math, connectors need careful path calculation |
| B. D3.js (client-rendered) | Battle-tested tree layout (d3-hierarchy), smooth zoom/pan built-in | ~180KB new dependency, paradigm shift from HTMX |
| C. CSS flexbox tree + JS pan/zoom | Minimal JS, leverages CSS | Limited visual fidelity, connectors hard to draw, no native zoom |
| D. Hybrid: server-calculated layout + HTML divs + SVG connectors | Reuses existing Razor rendering, clean separation | More moving parts than pure SVG |

**Decision**: **A — Custom SVG rendered server-side, with vanilla JS for pan/zoom and context menus**

**Rationale**:
- The project already renders Razor pages server-side; extending this to render an SVG document fits the existing pattern
- SVG supports native zoom/pan via `viewBox` manipulation and CSS `transform` on the `<svg>` element
- No new JavaScript dependencies aligns with "fewest moving parts" guidance
- Tree layout algorithm (simplified Reingold-Tilford) can be implemented in C# as a layout service, keeping the heavy lifting server-side
- HTMX can swap the SVG content for dynamic updates (add child, edit) without full reload
- The existing `_Layout.cshtml` uses inline styles; SVG can be styled similarly

**Implementation notes**:
- Implement a `TreeLayoutService` in Management/Application that computes (x, y) positions for each node using a top-down tree layout algorithm
- Render SVG with `<g>` groups per node (rectangles + text) and `<path>` elements for connectors
- Vanilla JS handles: pan (mouse drag), zoom (mouse wheel + buttons), right-click context menu, HTMX triggers for actions
- SVG `viewBox` adjusted for zoom; CSS `transform: translate()` for pan

## Decision 2: Node Data Shape for Chart

**Question**: What data does each chart node need beyond the existing `Organization` entity?

**Finding**: The existing `OrganizationService.ListAllAsync()` returns flat organizations. For chart rendering, we need a pre-laid-out tree with computed positions.

**Decision**: Add a `GetChartTreeAsync()` method to `OrganizationService` that:
1. Fetches all active organizations (or scoped to admin's org subtree for OrgAdmin)
2. Builds a tree structure in memory
3. Runs a tree layout algorithm to assign `(x, y)` coordinates
4. Returns a flat list of `OrgChartNodeDto` with layout positions, children references, and summary data (user count, course count, disabled status)

**DTO shape**:
```csharp
public record OrgChartNodeDto(
    Guid Id,
    string Name,
    string? Description,
    int Depth,            // Tree level for indentation
    int X,                // Computed layout X position
    int Y,                // Computed layout Y position
    bool IsDisabled,      // IsDeleted flag
    bool IsRoot,          // Has no parent
    int UserCount,        // Learners in this org
    int CourseCount,      // Courses in this org
    bool HasChildren,     // Whether expand/collapse UI needed
    Guid? ParentId
);
```

## Decision 3: Pan/Zoom Implementation

**Question**: How to implement smooth pan and zoom in the browser without a library?

**Finding**: SVG elements support CSS `transform` for both scaling and translation. Mouse wheel events provide zoom delta; mouse drag events provide pan delta.

**Decision**: Vanilla JS module (`wwwroot/js/org-chart.js`):
- Zoom: Listen to `wheel` events and button clicks; adjust a `scale` variable; apply via `transform: scale()` on the SVG group
- Pan: Listen to `mousedown` + `mousemove` + `mouseup` for drag; adjust `translateX/Y` variables; apply via `transform: translate()`
- Auto-fit: On initial load, compute the bounding box of all nodes and set the SVG `viewBox` to encompass them with padding
- CSS transitions for smooth animation: `transition: transform 0.2s ease-out`

**Alternatives considered**: Using the HTML5 Canvas API for rendering. Rejected because SVG provides built-in hit detection for right-click context menus (each node is an interactive `<g>` element).

## Decision 4: Context Menu Implementation

**Question**: How to implement right-click context menus on SVG nodes?

**Finding**: SVG `<g>` elements can receive `contextmenu` events. A custom HTML menu can be positioned absolutely at the click coordinates.

**Decision**: 
- Each SVG node `<g>` has a `data-org-id` attribute and listens for `contextmenu` events
- On right-click, prevent default browser menu and show a custom HTML `<ul>` positioned at cursor coordinates
- Menu items use HTMX attributes (`hx-post`, `hx-confirm`) to trigger server actions
- Menu auto-dismisses on outside click or ESC key

**HTMX wiring examples**:
- Edit: `hx-get="/Admin/Organizations/Chart/EditDialog?id=..." hx-target="#modal-container"`
- Disable: `hx-post="/Admin/Organizations/Chart/Disable?id=..." hx-confirm="Disable this org and all its descendants?" hx-swap="outerHTML"`
- Add User: `hx-get="/Admin/Organizations/Chart/AddUserDialog?orgId=..." hx-target="#modal-container"`

## Decision 5: OrgAdmin Scope Enforcement

**Question**: How to restrict OrgAdmin users to their own organizational subtree in the chart?

**Finding**: The existing `OrganizationService.GetSubtreeAsync()` already computes all descendants. The `DashboardService.GetOrgMetricsAsync()` already scopes queries by descendant org IDs.

**Decision**: 
- For SuperUser: `GetChartTreeAsync()` returns the full hierarchy from root
- For OrgAdmin: `GetChartTreeAsync()` takes the admin's org ID as root and only returns that subtree
- The page model determines the admin's role and scope from `HttpContext.User` claims, passing the appropriate root ID to the service
- This follows the same pattern already established in User Story 2 of spec 009

## Decision 6: Disable vs Delete Semantics

**Question**: The spec says "disable" — does this map to the existing `IsDeleted` soft-delete flag?

**Finding**: The `Organization` domain model has `IsDeleted` (soft delete). The `OrganizationService.DeleteAsync()` sets `IsDeleted = true`. However, "disable" in the spec implies a reversible action where the org still exists but is inactive.

**Decision**: Introduce a new `IsDisabled` boolean property on the `Organization` entity, separate from `IsDeleted`. This allows:
- Disabling: sets `IsDisabled = true` (reversible, org still queryable)
- Deleting: sets `IsDeleted = true` (existing behavior, permanent removal)
- Chart queries filter by `!IsDeleted` (same as before) but show disabled nodes visually distinct
- A disabled org cascades `IsDisabled` to all descendants (matching spec requirement)

**Migration**: Add `IsDisabled` column to `Organizations` table via EF Core migration. Default `false`.

## Decision 7: Summary Metrics on Nodes

**Question**: What summary data should each node display?

**Decision**: Each node shows:
- Organization name (primary label)
- Badge: user count (from `EnrollmentDbContext.Students` where `OrganizationId` matches)
- Badge: course count (from `CatalogDbContext.Courses` where `OrganizationId` matches)
- Visual indicator: disabled state (dimmed/grayed out)

These counts are computed in `OrganizationService.GetChartTreeAsync()` by joining across contexts (same pattern as `DashboardService`).

## Summary of Resolved Unknowns

| Unknown | Resolution |
|---------|-----------|
| Chart rendering technology | Custom SVG, server-rendered, vanilla JS for interaction |
| Tree layout algorithm | Simplified Reingold-Tilford in C#, server-side |
| Pan/zoom approach | CSS transform on SVG group, vanilla JS event handlers |
| Context menu | Custom HTML overlay triggered by SVG `contextmenu` event |
| Admin scope | OrgAdmin sees own subtree; SuperUser sees full tree |
| Disable semantics | New `IsDisabled` property on Organization (distinct from `IsDeleted`) |
| Node metrics | User count + course count badges per node |
