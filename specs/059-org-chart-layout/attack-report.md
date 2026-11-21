# Attack Report — 059 Org Chart Layout

**Role**: RED TEAM (Principle XIX) — attack the implemented code.
**SUT**: `src/Modules/Management/Application/TreeLayoutService.cs`
**Branch**: `spike/speckit-verification`
**Attack suite**: `tests/Management.Tests/Attack/` (13 tests, all new files)
**Rules of engagement**: spec 059 (L1–L6, Acceptance Examples 1–10, INV-1…INV-7, Abuse & Edge Cases table)

## Result summary

| | Count |
|---|---|
| **Confirmed breaks** | **1** (B1, wrong-output) |
| Unconfirmed-suspected | 0 |
| Spec-gaps found | 2 (G1, G2) |
| Attack angles that PASS (hold up) | 12 tests across 12 angles |

Attack suite: **12 passed / 1 failed** — the single failure is the confirmed break B1.
Pre-existing oracle suites untouched and still green: visible + holdout **22/22** (full project 121/122, the one failure being B1's attack test).

---

## Confirmed breaks

### B1 — L3 per-node rounding is deferred to one final rounding → off-by-one parent X  [severity: wrong-output]

- **Test**: `tests/Management.Tests/Attack/01_L3PerNodeRounding.cs` → `R_is_centered_over_Cs_rounded_X` — **FAILS on current code: expected R.X = 507, actual 506.**
- **Spec refs**: L3 (its rounding clause), L6 (shift happens *after* per-node rounding), FR-1, Assumption "Rounding: nearest integer, .5 away from zero (L3)". Not an INV-1…7 violation (no collision/overlap results — R's rectangle collides with nothing); it is an exact-coordinate contract violation of L1–L6.
- **Input** (valid L4 fixture, all edges wired both sides):
  `R → [C, H]; C → [F, G]; G → [G1, G2]`; leaves F, G1, G2, H.
  L2 widths: G = 510, C = 780, R = 1050.
- **Expected (spec, sequential L3 then L6)**:
  - F = 0; G1 = 270, G2 = 540 → G = (270+540)/2 = **405**
  - C = (0+405)/2 = 202.5 → L3 round (.5 away from zero) → **203**
  - H = 810 (leaf at frame 780+30)
  - R = midpoint of first/last child's **X** = (203+810)/2 = 506.5 → L3 round → **507**
  - L6: min X = 0 (F) → shift 0. Output: R(507,0,0), C(203,130,1), F(0,260,2), G(405,260,2), G1(270,390,3), G2(540,390,3), H(810,130,1).
- **Actual**: C = 203 (matches — 202.5 rounds to 203 either way), but **R = 506**.
- **Root cause** (in the implementation, per its own comment "shift first, round once"): all X values are carried as unrounded `double` through `PlaceSubtree`; the single `Math.Round(node.X − min, AwayFromZero)` in `CollectResults` happens at the end. The parent's midpoint is computed from the **unrounded** child X: R = (202.5 + 810)/2 = 506.25 → 506. The spec's L3 makes every node's X an integer *at placement time*, so a parent centers over rounded child values; L6 then shifts integers.
- **Why the oracle missed it**: every Acceptance Example value is integer-exact by construction (row 9 is explicitly "no fractional case"; rows 1–8 midpoints are integers). The divergence requires a shape with a **nested fractional midpoint**: a child whose midpoint is x.5 whose parent's midpoint is also fractional — i.e., a parent over a fractional child + an integer child. C itself matches; only the parent (R) diverges, so a partial-coordinate fixture would not catch it.
- **Reproduction**: the attack test above; confirmed in round 1 (single confirmation attempt needed).

**Fix note (for the implementer — not applied by RED TEAM)**: round each node's X to int at placement in `PlaceSubtree` (L3), carry ints forward, and let L6 shift integers; or otherwise make `R = round((round(C) + H)/2)`.

---

## Unconfirmed-suspected

**None.** Every suspected break was either confirmed (B1) or resolved as a passing angle (below) or as a spec-gap (G1/G2). No break class consumed its 2-attempt budget.

---

## Spec-gaps (for the user — spec is silent; no preference encoded in tests)

### G1 — an org that is BOTH a root and a child (shared parent, ParentId = null): which identity wins?

- **Angle**: `07_RootAlsoChild.cs` (both input orders) — **PASSES**: the org renders **exactly once** (INV-6) and terminates (INV-7) in both orders; when both identities render as roots, L5 spacing (270) holds.
- **Gap**: L4 makes the org a root (ParentId null), and the `Children` collection makes it a child; the spec never states which rendering wins (as a root at depth 0, or as the child at depth 1). The implementation gives priority to the **first encounter in input order** (order A `[P, R]` → R renders as P's child; order B `[R, P]` → R renders as a root and P's edge to R is cut). The test only asserts the spec-mandatory parts (exactly-once, termination, L5 spacing, L6 order of the rendered roots) — it does **not** pin the priority, because the spec does not.
- **Suggested spec amendment**: define winner priority (e.g., "root identity wins" or "input-order first encounter wins") in L4/L6.

### G2 — DAG: a non-deleted org in two different parents' Children collections (shared child)

- **Angle**: `12_SharedChildDag.cs` — **PASSES** for the spec-mandatory part: terminates ≤ 5 s, S rendered exactly once (INV-6), P1/P2 each once, no coordinate collision (INV-1), no rectangle overlap (INV-2), Y = depth×130 (INV-3), min X = 0 (INV-4).
- **Gap**: L4 presumes a tree ("the tree is defined by the Children collections"). For a DAG the spec does not define how the **non-owning parent's width/centering** must treat the cut child. The implementation removes the cut child from the non-owning parent's `Children` entirely, so that parent's L2 width excludes it (P2 becomes a lone leaf). The test deliberately does **not** assert P2's X for that reason.
- **Suggested spec amendment**: either state that such input is an input-contract violation (like duplicate Ids) or define the width/centering rule.

*(Duplicate-Id input, per the abuse table, is explicitly "do not assert a specific output" — termination only; the implementation terminates. Not a gap.)*

---

## Attack angles that PASS (what holds up)

| # | Angle (spec ref) | Test | Result |
|---|---|---|---|
| 1 | 3-level asymmetric tree, sibling subtree widths 240 vs 780 — parent centering + sibling spacing exact per L2/L3; INV-1/2/3/4 | `02_Asymmetric3Level` | **PASS** — P(270,0), M(0,130), m(0,260), S(540,130), s1(270,260), s2(540,260), s3(810,260) exactly as hand-computed |
| 2 | Node whose single child is an internal subtree (width 510 > 240) + exactly-240 chain edge (parent/child share X, Y 130 apart → 80 gap) | `03_SingleChildInternalSubtree` | **PASS** — P(135,0), M(135,130), m1(0,260), m2(270,260); no overlap; P's 240-wide rect fits its subtree span |
| 3 | 1000-deep chain — termination, no stack overflow (SUT recurses in 5 passes to full depth), INV-1/3/4/7 | `04_Chain1000` | **PASS** — completes in ~50 ms; N1(0,0,0), N500(0,64870,499), N1000(0,129870,999); all (X,Y) distinct |
| 4 | 3-cycle A→B→C→A with **all three also roots** (most hostile L4 reading) — row 10 generalized; INV-6/7 | `05_TripleCycle` | **PASS** — terminates; each org rendered exactly once; chain layout A(0,0,0), B(0,130,1), C(0,260,2); cycle entry (C's child A) cut |
| 5 | Self-cycle (S.Children = [S]) — row 10 generalized | `06_SelfCycle` | **PASS** — terminates; S rendered exactly once at (0,0,0); self-child entry cut |
| 6 | Root also listed as someone's child, **both input orders** — exactly-once + termination + L5 spacing (spec-mandatory part; see G1) | `07_RootAlsoChild` (2 tests) | **PASS** — order A: P(0,0,0), R(0,130,1); order B: R(0,0,0), P(270,0,0) |
| 7 | All roots soft-deleted → empty output (row 8 + L4 generalized to a forest) | `08_AllRootsDeleted` | **PASS** — empty output |
| 8 | Self-referential ParentId (O.ParentId = O.Id, O in input, in nobody's Children) — L4 literal: not a root, not reachable → not rendered | `09_SelfParentId` | **PASS** — only R(0,0,0) rendered |
| 9 | Mixed sibling widths 240/510/780 under one parent — L2 right-edge chaining, L3 centering, INV-1/2/3/4 | `10_MixedWidths` | **PASS** — c1(0,130), c2(405,130), c3(1080,130), c21(270,260), c22(540,260), c31(810,260), c32(1080,260), c33(1350,260), P(540,0) exactly as hand-computed |
| 10 | `Children` containing an org not in the flat list → excluded (abuse-table row; not covered by oracle suites) | `11_ChildNotInFlatList` | **PASS** — Q excluded; R(0,0,0) only |
| 11 | Shared-child DAG — exactly-once + termination + no overlap (spec-mandatory part; see G2) | `12_SharedChildDag` | **PASS** |
| 12 | Deterministic behavior re-checked on B1 fixture — the *other* six coordinates (C, F, G, G1, G2, H) match the spec exactly; only R diverges, confirming the failure is a single rounding-order deviation, not a structural layout defect | `01_L3PerNodeRounding` (non-failing assertions) | **PASS** (for those fields) |

**Structural note (why no INV-1/INV-2 break was found):** min X is always the first root's frame start (a leftmost leaf), so the L6 shift is a no-op and rounding can never pull two same-depth nodes together — same-depth nodes always sit in frame spans ≥ 270 apart (240 width + 30 gap), and round is 1-Lipschitz. The B1 defect is confined to a parent's own midpoint being computed from an unrounded child X.

---

## Gate status

- Attack suite: **RED** (1 confirmed break: B1).
- Visible + holdout oracle suites: **GREEN 22/22** (unchanged; no oracle file modified).
- Per Principle XIX, the fix for B1 belongs to the implementer; RED TEAM made no changes to `src/` or to any pre-existing test file.
