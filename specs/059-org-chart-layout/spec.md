# Feature Specification: Org Chart Layout — Collision-Free Positioning for Arbitrary Org Shapes

**Feature Branch**: `story/059-org-chart-layout` (spike execution on `spike/speckit-verification`)

**Created**: 2026-11-21

**Status**: Draft

**Input**: User description: "The org chart (spec 013) draws organization nodes on top of
each other for any shape with two or more siblings under a node, because the layout
algorithm positions sibling subtrees by the sibling's *root* coordinate instead of the
previous subtree's *right edge*, and centers the parent after (overwriting) the sibling
offset. Spec 059 defines the layout contract precisely, makes the expected values
computable from the spec alone, and requires the fix to satisfy it — verified by the
verification-first gate stack (Principles XVIII/XIX: oracle → implement → attack →
mutate)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The org chart never draws two nodes on top of each other (Priority: P1)

An admin views the org chart (spec 013). For any organization shape — a chain, a
wide fan-out, a two-level grid, a forest of disconnected roots — every node is
drawn at a distinct position: no two nodes share a coordinate and no two node
rectangles overlap. Today, a shape with two nodes each having two children renders
four pairs of overlapping nodes (verified by code trace of
`TreeLayoutService.FirstWalk/SecondWalk`; the existing 058 unit tests do not cover
multi-sibling shapes, which is why the defect reached the page).

**Why this priority**: P1 — user-visible, wrong on every affected page load, and
the whole point of this spec: a *precise layout contract* whose expected values a
test author can compute without seeing the implementation.

**Independent Test**: Feed `ComputeLayout` the spec's fixtures (single node,
3-chain, 2×2 grid, forest, deleted child, empty, 1000-node fan-out) and assert
the exact coordinates from the Acceptance Examples table plus the invariants
(no shared `(X,Y)`, no rectangle overlap, `Y = depth×130`, min `X = 0`).

**Acceptance Scenarios**:

1. **Given** the 2×2 sibling fixture (two nodes each with two children), **When** the layout is computed, **Then** all seven coordinates match the spec table exactly and no two node rectangles overlap.
2. **Given** a forest of two disconnected roots (one leaf, one with a child), **When** the layout is computed, **Then** the roots are separated by the sibling gap and no root pair shares an origin.
3. **Given** a cyclic input (A's Children include B, B's Children include A), **When** the layout is computed, **Then** the computation terminates and each cyclic org renders once, with the cycle entry cut (no infinite recursion).

## Edge Cases

- Single node with no children → exactly one entry at (0, 0, depth 0).
- Empty input → empty output.
- Soft-deleted children are excluded from the layout (spec 058-pinned behavior).
- Dangling `ParentId` (parent not in the input list) → the org is a root.
- A root-level org with `IsDeleted = true` → not rendered (new in 059; today it renders).
- Deep chains (500) and wide fan-outs (1000) → terminate, no stack overflow.
- Input order of `Children` determines left-to-right sibling order.

## Acceptance Examples *(mandatory)*

<!-- Expected values computed from the layout contract (Invariants section, rules L1–L6)
     alone. [CONFIRMED] = matches behavior already pinned by spec 058 tests or trivially
     derivable. [NEEDS-USER] = user must confirm at the spec checkpoint. -->

Layout contract (the spec's deterministic placement rules — all expected values below
follow from these):

- **L1** `Y = depth × 130` (node height 50 + level gap 80). Depth 0 = root.
- **L2** A node occupies a 240-wide rectangle starting at its X. Sibling gap = 30:
  the next sibling's subtree starts at the previous sibling's **subtree right edge + 30**
  (subtree width = max(240, Σ child widths + 30 × (child count − 1))).
- **L3** A node with children is centered over them: its X is the midpoint of its
  first and last child's X (rounded to nearest integer, .5 away from zero).
  A leaf sits at its frame's start.
- **L4** The tree is defined by the `Children` collections (not `ParentId`). Roots =
  orgs whose `ParentId` is null **or** whose `ParentId` does not match any input org.
  Non-deleted children appear in `Children` order.
- **L5** Forest: multiple roots are laid out left-to-right in input order, each
  subtree starting at the previous subtree's right edge + 30.
- **L6** After layout, all X values are shifted so the minimum X is 0. Output order:
  pre-order per root, roots in input order. An org that is neither a root nor
  reachable through `Children` is not rendered.

| # | Input (fully concrete) | Expected output (exact value / status / message) | Confirmed |
|---|------------------------|--------------------------------------------------|-----------|
| 1 | One org `R` (no children) | `[(R, 0, 0, 0)]` | [CONFIRMED] |
| 2 | Chain `R→A→B` (each org's only child is the next) | `[(R, 0, 0, 0), (A, 0, 130, 1), (B, 0, 260, 2)]` — L3 centers a single child over itself; L6 normalizes | [NEEDS-USER] |
| 3 | **2×2 grid**: `R` has children `A, B`; `A` has children `A1, A2`; `B` has children `B1, B2` (all leaves) | `[(R, 405, 0, 0), (A, 135, 130, 1), (B, 675, 130, 1), (A1, 0, 260, 2), (A2, 270, 260, 2), (B1, 540, 260, 2), (B2, 810, 260, 2)]` — derived from L2 (A/B subtree width 510, B frame starts at 540) + L3 (R = midpoint(135, 675)); the pre-fix code produces duplicate coordinates here (A and B both 405; A1/B1 both 270; A2/B2 both 540) | [NEEDS-USER] |
| 4 | Forest: input `[R1, R2, C]`, `C` is `R2`'s only child, `R1` has none | `[(R1, 0, 0, 0), (R2, 270, 0, 0), (C, 270, 130, 1)]` — L5 (R2 frame at 240+30) + L3 | [NEEDS-USER] |
| 5 | `[R, A, D]`: `R`'s children `[A, D]`, `D.IsDeleted = true` | `[(R, 0, 0, 0), (A, 0, 130, 1)]` — D excluded (058-pinned); A is R's only *rendered* child, L3 centers R over A → R.x = 0 (pre-fix code yields R=135, A=270) | [CONFIRMED] |
| 6 | `[]` (empty input) | `[]` | [CONFIRMED] |
| 7 | `[R, O]`: `O.ParentId = R.Id` but `O` is not in `R.Children` (dangling reference, orphan unreachable) | `[(R, 0, 0, 0)]` — O is neither a root (parent in list) nor reachable → not rendered (L4/L6) | [NEEDS-USER] |
| 8 | `[R, D]`: `R` no children, `D.ParentId = null`, `D.IsDeleted = true` | `[(R, 0, 0, 0)]` — deleted root not rendered (new in 059) | [NEEDS-USER] |
| 9 | Fan-out: `R` with 1000 leaf children, input order C1…C1000 | Terminates; `C_i` at `X = (i−1)×270`, `Y = 130`; `R` at the midpoint of C1 and C1000 = `(0 + 999×270)/2 = 134857.5 → 134858` (L3, .5 away from zero); min-X shift = 0 | [CONFIRMED] (derived) |
| 10 | Cycle: `[A, B]`, `A.Children = [B]`, `B.Children = [A]` | Terminates (≤ 5 s); exactly two entries, no coordinate collision; the cycle entry is cut so neither org's in-cycle child is expanded | [NEEDS-USER] |

## Invariants *(mandatory)*

1. **INV-1 no-coordinate-collision**: for any valid input, no two output entries share `(X, Y)`. *Test strategy: FsCheck property over generated tree shapes (depth ≤ 4, width ≤ 6) + the spec's fixtures.*
2. **INV-2 no-rectangle-overlap**: for any valid input, no two 240×50 node rectangles `([X, X+240), [Y, Y+50))` overlap. *Test strategy: FsCheck property, same generators.*
3. **INV-3 y-follows-depth**: `Y = Depth × 130` for every entry. *Test strategy: FsCheck property + fixture row 2.*
4. **INV-4 min-x-is-zero**: for non-empty output, `min(X) = 0`. *Test strategy: FsCheck property.*
5. **INV-5 deterministic**: identical input (same `Children` order) yields identical output, every run. *Test strategy: FsCheck — run layout twice per generated tree, compare.*
6. **INV-6 completeness**: every non-deleted org reachable from a root appears exactly once; no other org appears. *Test strategy: FsCheck + fixture row 7/8.*
7. **INV-7 terminates**: every input — including cyclic ones — finishes within 5 s for ≤ 1000 orgs. *Test strategy: fixture (cycle, 500-chain, 1000-fan-out) with a wall-clock budget.*

## Abuse & Edge Cases *(mandatory)*

| Case | Required behavior (incl. failure mode) |
|------|----------------------------------------|
| Cyclic `Children` (A↔B) | Terminates ≤ 5 s; each cyclic org rendered once; cycle entry cut; no stack overflow (INV-7, row 10) |
| 500-node chain | Completes; no stack overflow; INV-1/INV-2 hold |
| 1000-node fan-out | Completes; siblings 270 apart; INV-2 holds; R centered per L3 |
| Empty `Children` / empty input | Normal single-node / empty output (rows 1, 6) |
| Root-level `IsDeleted = true` | Not rendered (row 8) |
| Dangling `ParentId` (orphan) | Rendered as a root when its parent is absent from the input (L4) — see row 7 for the unreachable variant |
| Duplicate `Id` in input | Input contract violation: no requirement beyond termination (documented undefined behavior — do not assert a specific output) |
| `Children` containing an org not in the flat list | Excluded (L4: children must be members of the input) |

## Requirements *(mandatory)*

### Functional Requirements

- **FR-1**: `ComputeLayout(IList<Organization>)` returns positions satisfying the layout contract L1–L6 for every input, deterministically.
- **FR-2**: No two entries share `(X, Y)`; no two 240×50 rectangles overlap (INV-1, INV-2).
- **FR-3**: Forest, orphan, soft-deleted, and cyclic inputs are handled per the Acceptance Examples and Abuse table (no non-termination on any input).
- **FR-4**: `Y = depth × 130` (L1); minimum X is 0 (L6); output order is pre-order per root, roots in input order (L6).

### Key Entities

- **Organization** (existing, `Management.Domain`): `Id (Guid)`, `Name`, `ParentId (Guid?)`, `Children (ICollection<Organization>)`, `IsDeleted`. Read-only input to the layout.
- **Layout result**: flat `IList<(Organization Org, int X, int Y, int Depth)>` — the public return shape (unchanged).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-1**: visible + holdout + attack suites all green after the fix (Principle XIX.1 gate line).
- **SC-2**: mutation gate on the diff: **zero surviving mutants** in `TreeLayoutService`; floor 80% / target 100% (starting gate, first known baseline).
- **SC-3**: all seven invariants pass as written (INV-1…INV-7; FsCheck properties with 100 samples each).
- **SC-4**: gate self-test catches 3/3 planted bugs (off-by-one, wrong operator, missing validation) — required because the mutation gate configuration is new for this repo (Principle XIX.5).
- **SC-5**: the org chart page (spec 013) renders the 2×2 fixture without overlapping nodes (manual/Playwright check, supporting evidence per Principle XIII).

## Assumptions

- The layout constants (node width 240, node height 50, sibling gap 30, level gap 80) are the current, user-visible values and remain pinned; a change to them is a spec amendment, not an implementation choice.
- The `Children` collections are the authoritative tree (populated by the caller/EF); `ParentId` is used only for root detection (L4).
- Rounding: nearest integer, .5 away from zero (L3).
- The pre-fix behavior (duplicate coordinates on multi-sibling shapes; unnormalized X; deleted roots rendered; non-termination on cycles) is the defect class this spec closes.
