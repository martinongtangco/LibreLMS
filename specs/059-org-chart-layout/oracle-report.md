# Oracle Report — 059 Org Chart Layout

Status: **RED state (pre-implementation) — barrier held**

## (a) Test inventory

### Visible suite — `tests/Management.Tests/OrgChartLayoutTests.cs` (15 tests)

| # | Test | Spec reference |
|---|------|----------------|
| 1 | `Acceptance_row1_single` | Row 1 |
| 2 | `Acceptance_row2_chain` | Row 2 |
| 3 | `Acceptance_row3_grid2x2` | Row 3 |
| 4 | `Acceptance_row4_forest` | Row 4 |
| 5 | `Acceptance_row5_deletedChild` | Row 5 |
| 6 | `Acceptance_row6_empty` | Row 6 |
| 7 | `Acceptance_row7_unreachableOrphan` | Row 7 |
| 8 | `Acceptance_row8_deletedRoot` | Row 8 |
| 9 | `Acceptance_row9_fanout1000` | Row 9 |
| 10 | `Acceptance_row10_cycle` | Row 10 |
| 11 | `Invariant_yFollowsDepth` | Invariant (Y = depth × 130; rows 3 + 9 fixtures) |
| 12 | `Invariant_minXIsZero` | Invariant (min X = 0; rows 1–9 fixtures) |
| 13 | `Invariant_noRectangleOverlap` | Invariant (no rectangle overlap; rows 2, 3, 4, 9) |
| 14 | `Abuse_500Chain` | Abuse (500-deep chain) |
| 15 | `Abuse_duplicateId` | Abuse (duplicate Id) |

### Holdout suite — `tests/Management.Tests/Holdout/OrgChartLayoutHoldoutTests.cs` (7 tests)

| # | Test | Spec reference |
|---|------|----------------|
| 1 | `Holdout_row2_chain_coordinates` | Row 2 (per-field decomposition; entries selected by Name) |
| 2 | `Holdout_row3_grid2x2_noSharedCoordinates` | Row 3 + overlap invariant (all C(7,2) = 21 pairs, explicit loop) |
| 3 | `Holdout_row4_forest_rootSeparation` | Row 4 (gap property: R2.X − R1.X = 270) |
| 4 | `Holdout_invariant5_deterministic` | Invariant 5 (determinism: two runs on one fixture, element-identical X/Y/Depth/order) |
| 5 | `Holdout_invariant6_completeness` | Invariant 6 (completeness: 2×2 → all 7 names; row-7 fixture → {R} only) |
| 6 | `Holdout_abuse_cycle_bounded` | Row 10 / abuse (cycle bounded to 5 s via `Task.Run` + `Wait(5000)`) |
| 7 | `Holdout_abuse_fanout1000_extremes` | Row 9 / abuse (extremes: C1.X = 0, C500.X = 499×270 = 134730, C1000.X = 999×270 = 269730, R.X = 134865) |

> **Note — holdout spec slip corrected during authoring (Principle XVIII):** the holdout directive
> stated `C1000.X == 269700`. That value is arithmetically inconsistent with the corrected root
> value in the same directive: `2 × 134865 = 269730 = 999 × 270`, and the corrected red-state
> run confirmed every `C_i` lands at `(i−1) × 270` (C1000 = 269730, which passed pre-fix).
> The holdout test therefore asserts `999 * 270` (= 269730), keeping the suite green-compatible.

## (b) RED-STATE — visible suite (pre-implementation, L4-corrected fixtures)

Run: 14 tests in one pass + row 10 isolated (row 10 crashes the test host; a stack overflow
is uncatchable, so the combined run aborts). **Result: 8 passed / 7 failed (of 15).**

**Passed (8):**

1. `Acceptance_row1_single`
2. `Acceptance_row2_chain`
3. `Acceptance_row5_deletedChild`
4. `Acceptance_row6_empty`
5. `Acceptance_row7_unreachableOrphan`
6. `Invariant_yFollowsDepth`
7. `Invariant_minXIsZero`
8. `Abuse_500Chain`

**Failed (7):**

1. `Acceptance_row3_grid2x2` — count 7 matches; root `R` X: actual **270**, expected **405** (root not placed at the midpoint of its children's span 135–675)
2. `Acceptance_row4_forest` — count 3 matches; `R2` X: actual **0**, expected **270** (roots not spaced; all roots land at x = 0)
3. `Acceptance_row8_deletedRoot` — count: actual **2**, expected **1** (deleted root `D` not filtered out)
4. `Acceptance_row9_fanout1000` — count 1001 matches (all `C_i` positions pass); root `R` X: actual **134865**, expected **134858** per original spec. **Spec defect (Principle XVIII):** 999 × 270 / 2 = 134865 exactly, so the spec value 134858 was an arithmetic error and has been **corrected to 134865**. After the correction, row 9 passes pre-implementation for the root.
5. `Acceptance_row10_cycle` — **test host crash**: Stack Overflow from unguarded recursion in `TreeLayoutService.BuildLayoutNode`. Recorded as RED (crash = failure).
6. `Invariant_noRectangleOverlap` — row 3: `A1` `[0,240)×[260,310)` and `B1` `[0,240)×[260,310)` intersect (B's subtree laid out at the same x = 0 as A's subtree — no sibling-subtree offset)
7. `Abuse_duplicateId` — unhandled `System.ArgumentException: An item with the same key has already been added` from `ToDictionary` keyed by `Id` (`TreeLayoutService.cs:50`). Recorded as RED (crash = failure).

**Host-crash note:** rows 10 and 15 (duplicate id) both kill the test host pre-fix (stack overflow /
unhandled `ArgumentException` propagating out of the SUT); per the red-state convention, crash = failure.

## (c) RED-STATE — holdout suite (pre-implementation)

Combined run (`--filter "FullyQualifiedName~OrgChartLayout"`, visible + holdout): the pre-fix cycle
crash killed the host, so most tests never produced a result. Failures recorded before the abort:
`Holdout_row3_grid2x2_noSharedCoordinates`, `Acceptance_row4_forest`, `Holdout_row4_forest_rootSeparation`,
`Abuse_duplicateId`, `Acceptance_row9_fanout1000`. All other tests in the combined run never ran
(aborted before the summary; no pass/fail recorded).

To characterize the holdout fully, the holdout was then run in isolation (cycle test excluded, same
method as the visible suite): **4 passed / 2 failed (of 6)**, plus the cycle test = **host crash** (red).

**Passed (4):**

1. `Holdout_row2_chain_coordinates`
2. `Holdout_invariant5_deterministic`
3. `Holdout_invariant6_completeness`
4. `Holdout_abuse_fanout1000_extremes` — pre-fix the SUT already places `C1.X = 0`, `C500.X = 134730`, `C1000.X = 269730`, `R.X = 134865`, so all four extreme probes pass pre-implementation

**Failed (2):**

1. `Holdout_row3_grid2x2_noSharedCoordinates` — `(X,Y) shared by 'A1' and 'B1' at (0,260)` (first of the 21 pairs checked; B's subtree collides with A's)
2. `Holdout_row4_forest_rootSeparation` — `R2.X − R1.X`: actual **0**, expected **270**

**Crash (1, recorded red):**

3. `Holdout_abuse_cycle_bounded` — `Task.Run` + `Wait(5000)` cannot bound the pre-fix behavior: the SUT's unguarded recursion in `BuildLayoutNode` raises an uncatchable Stack Overflow that terminates the test host instead of letting the test assert the timeout

## (d) Barrier declaration

No implementation file was read. The oracle was built from, and verified against, spec.md, the plan's
Public Interface section, and the `Organization.cs` / `Entity.cs` type shapes only.
