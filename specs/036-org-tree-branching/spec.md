# Feature Specification: Organization Tree Branching in Admin Organizations

**Feature Branch**: `story/036-org-tree-branching`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2026-08-24

**Status**: Complete (merged 2026-08-24)

**Input**: User description: "While testing the Admin > Organizations, i discovered that theres no actual branching and root visible enough to identify if a two nodes are under the same root. For example, i creted Finance and Sales under "Root Organization" (top org). Then I created Billing under Finance. All Finance, Billing, and Sales are rendered to be on the same indentation together. There should be a tree like branching and node visible to know who is the parent of what. Design/UX should be modern and matching our design decisions. Things should be tracable and branched"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See the Organization Hierarchy at a Glance (Priority: P1)

An admin opens Admin > Organizations and sees every organization rendered as a hierarchical tree instead of a flat, uniformly-indented list. Each organization is a node row; child organizations are indented one level deeper than their parent; visible branch/connector lines run from each parent down to each of its children; and the root organization is visually distinguished (a clear "Root" indicator and stronger styling). With the tree rendered this way, an admin can tell at a glance who is under whom.

**Why this priority**: This is the core of the reported problem — today all non-root nodes render at the same indentation with no branch lines, so the hierarchy (which parent owns which node) is invisible. Until the tree structure itself is visible, no other refinement matters.

**Independent Test**: Seed a hierarchy (Root Organization → Finance, Sales; Finance → Billing), open Admin > Organizations, and verify: Billing sits one level deeper than Finance and Sales; connector lines link Root→Finance, Root→Sales, and Finance→Billing; the root row is visually distinct from all others.

**Acceptance Scenarios**:

1. **Given** the hierarchy Root → Finance, Sales and Finance → Billing exists, **When** the admin opens Admin > Organizations, **Then** Finance and Sales render at the same indentation one level deeper than the root, and Billing renders one level deeper than Finance
2. **Given** the tree is displayed, **When** the admin looks at any non-root node, **Then** a visible connector line can be traced from that node up to its parent node without ambiguity
3. **Given** the tree is displayed, **When** the admin looks at the root organization row, **Then** it is clearly distinguishable from child rows by a root indicator and distinct styling
4. **Given** an organization has no children, **When** the tree is displayed, **Then** no dangling or orphaned connector lines are drawn below it
5. **Given** organizations exist in the admin's scope, **When** the page loads, **Then** every organization appears exactly once in the tree, with no missing or duplicated nodes

---

### User Story 2 - Trace Parent and Sibling Relationships (Priority: P2)

An admin can determine the relationship between any two organizations on the page by looking at the tree: whether one is the parent of the other, whether they are siblings (children of the same parent), or whether they are unrelated. Sibling organizations are grouped visually under their shared parent, so it is obvious that Finance and Sales share the root as a parent while Billing belongs only under Finance.

**Why this priority**: The user's concrete complaint is not just "I can't see depth" but "I can't tell if two nodes are under the same root." Traceability of parent/child and sibling relationships is the measurable outcome this feature must deliver.

**Independent Test**: Using the same seeded hierarchy, ask an admin to state (a) Billing's parent, (b) whether Finance and Sales share a parent, and (c) Billing's relationship to Sales — all from the tree view alone, without opening the interactive chart or any edit screen.

**Acceptance Scenarios**:

1. **Given** Finance and Sales are both children of the root, **When** the admin views the tree, **Then** both nodes are grouped at the same indentation under the root's branch, making their shared parent visually obvious
2. **Given** Billing is a child of Finance only, **When** the admin views the tree, **Then** Billing's connector line traces to Finance, not to the root or to Sales, so Billing is never misread as a sibling of Finance or Sales
3. **Given** two organizations with no ancestor relationship (e.g., Billing and Sales), **When** the admin compares them, **Then** their separate branch lines make it visually clear they belong to different subtrees

---

### User Story 3 - Manage Organizations From the Tree (Priority: P3)

An admin performs the same organization management they do today — opening Edit for a node, creating a new organization, opening the interactive Org Chart — from the tree view, with no loss of existing capability. Disabled organizations and all of their descendants are shown visually distinct from active ones, consistent with the existing product rule that disabled orgs remain listed but stand apart.

**Why this priority**: The tree must not degrade the existing management workflow while it fixes the visibility problem. Preserving actions and the disabled-state distinction is essential but is a constraint on the P1/P2 work rather than new value on its own.

**Independent Test**: From the tree view, open Edit on any node and confirm the edit screen behaves as today; click Create Organization and confirm creation still works and the new node appears in the correct tree position on reload; open a hierarchy containing a disabled organization and confirm it and its descendants are visually distinct from active rows.

**Acceptance Scenarios**:

1. **Given** the tree is displayed, **When** the admin clicks Edit on any node row, **Then** the existing edit screen opens for that organization with no change in behavior
2. **Given** the admin creates a new organization under an existing parent, **When** they return to the Organizations page, **Then** the new organization appears as a child of that parent in the tree with correct indentation and connector
3. **Given** an organization is disabled, **When** the tree is displayed, **Then** that organization and every one of its descendants render with a distinct disabled appearance (e.g., muted styling and/or a disabled indicator) while remaining visible in the tree
4. **Given** the tree is displayed, **When** the admin uses the "Create Organization" and "Org Chart View" entry points, **Then** both work exactly as they do today

---

### User Story 4 - Tree Stays Usable on Small Screens and Deep Hierarchies (Priority: P4)

An admin on a phone or tablet (viewport width down to 375px) can still read the full hierarchy with no horizontal scrolling and no overlapping branch lines; indentation may compress on small screens but the parent/child structure stays discernible. For deep hierarchies (6 or more levels), every level remains readable — the visual indentation does not push node content off-screen or into an unreadable sliver.

**Why this priority**: The project has an established mobile-responsive baseline (≤760px hamburger nav, no horizontal scrolling), and org hierarchies can grow deep over time. This protects the P1–P3 value on smaller screens and longer-lived data rather than adding new capability.

**Independent Test**: Load the Organizations page at a 375px viewport with a 7-level seeded hierarchy and confirm: no horizontal scroll, all rows legible, branch lines not overlapping, and the deepest node still clearly traceable to its parent.

**Acceptance Scenarios**:

1. **Given** a 7-level organization hierarchy, **When** the admin opens the page at any viewport width from 375px to 1440px, **Then** the page has no horizontal scrolling and every node row's name is fully readable
2. **Given** a parent with many children (e.g., 15+ siblings), **When** the tree is displayed, **Then** all sibling rows render at the same indentation with their connectors intact
3. **Given** an organization with a long name, **When** the tree is displayed, **Then** the name does not break the branch lines or push other rows out of alignment

## Edge Cases

- **Only the root exists**: a single-node tree renders the root row with its root indicator, no connector lines, and no empty placeholder rows.
- **Single root rule**: the existing business rule (exactly one root organization) is unchanged; the tree assumes and renders exactly one root.
- **Deep nesting (6+ levels)**: indentation must remain readable — the visual indent per level may be capped or the tree may scroll within its own region, but content must never be pushed off-screen.
- **Many siblings**: 15+ children of one parent must all render aligned under the same branch with intact connectors.
- **Long organization names**: names longer than typical must not break layout or connector alignment.
- **Missing description**: nodes without a description render cleanly (no stray punctuation or empty description block).
- **Leaf nodes**: nodes with no children must not show dangling connectors or phantom expand affordances.
- **Disabled organizations**: a disabled org and all of its descendants stay visible but visually distinct; disabling a mid-tree org visibly affects the whole subtree.
- **Special characters in names**: organization names render as plain text (no markup injection, no broken layout).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST render every non-deleted organization in the admin's scope exactly once in the Organizations view as a node in a hierarchical tree.
- **FR-002**: The system MUST indent each node proportionally to its depth: the root at depth 0, each child one visual level deeper than its parent.
- **FR-003**: The system MUST draw visible branch/connector lines from each parent node to each of its child nodes so that any node's parent can be identified by tracing its line alone.
- **FR-004**: The system MUST render sibling organizations (same parent) at identical indentation, visually grouped under their shared parent's branch.
- **FR-005**: The system MUST visually distinguish the root organization from all other nodes with an explicit root indicator and stronger styling.
- **FR-006**: Each node row MUST display the organization's name, its description (when present), and the same management actions the Organizations view offers today (at minimum: Edit).
- **FR-007**: The system MUST render disabled organizations and all of their descendants with a distinct disabled appearance while keeping them visible in the tree.
- **FR-008**: The system MUST render the tree using the project's existing design system tokens (palette, typography, border radii, spacing) so the result matches established design decisions; no off-system colors, fonts, or shapes.
- **FR-009**: At viewport widths from 375px upward, the tree MUST be fully readable with no horizontal scrolling; per-level indentation may compress on small screens but the hierarchy must remain discernible.
- **FR-010**: For hierarchies of 6 or more levels of depth, all nodes MUST remain reachable and readable (e.g., by capping visual indentation or providing a contained scroll region) without pushing content off-screen.
- **FR-011**: The "Create Organization" and "Org Chart View" entry points MUST remain on the page and retain their current behavior.

### Key Entities

- **Organization**: A node in the hierarchy. Attributes: name (unique within its parent), optional description, parent (none for the root), zero or more children, creation date, active/disabled state. Self-referential parent/child relationship; the hierarchy contains exactly one root. (Existing entity — this feature changes only how it is displayed, not its definition.)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can identify the parent of any organization on the page in under 5 seconds using the tree alone, without opening the interactive chart or any edit screen.
- **SC-002**: In the standard test hierarchy (Root → Finance, Sales; Finance → Billing), 100% of admin test participants correctly state, from the tree alone, that Billing's parent is Finance, that Finance and Sales are siblings under the root, and that Billing is not related to Sales.
- **SC-003**: All existing organization management actions (view, edit, create, open chart) complete with the same success rate as before this change — zero functional regressions.
- **SC-004**: The tree renders at viewport widths from 375px to 1440px with zero horizontal scrolling and zero overlapping or broken connector lines.
- **SC-005**: A visual review confirms the tree uses only existing design-system tokens (colors, fonts, radii, spacing) — zero off-system visual elements.

## Assumptions

- **Single root is enforced already**: the existing "exactly one root organization" business rule remains in force; the tree view assumes one root and does not add multi-root support.
- **Fully expanded tree, no collapse**: the tree renders fully expanded by default. Expand/collapse toggles are out of scope for this slice because the goal is at-a-glance traceability; org counts in this system are small (tens to low hundreds), so full expansion stays readable.
- **List/tree view only**: the interactive Org Chart view (spec 013) is a separate, existing surface and is out of scope here; this feature improves the standard Organizations page only.
- **Actions preserved, not expanded**: per-node actions are unchanged from today (Edit on each node; Create Organization and Org Chart View as page-level entry points). Adding new per-node actions (e.g., add-child, disable, assign user) to the tree is a future enhancement, not part of this slice.
- **Soft-deleted orgs stay excluded**: deleted organizations are already filtered out of the list; they remain hidden in the tree.
- **Small data scale**: no virtualization, lazy loading, or pagination is needed at the expected organization counts.
- **Disabled-state styling is a visual distinction only**: the disabled appearance (muted styling and/or indicator) does not change any behavior of disabled organizations.
