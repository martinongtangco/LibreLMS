# Mutation Report — spec 059 (gate run 2026-11-21)

## Gate verdict line (Principle XIX.1)

`MUTATION GATE: PASS-WITH-DOCUMENTED-EXCEPTION — diff-scope score 79.1% strict (81.4% with
timeout-as-kill), 7 survivors in-diff (all structurally vacuous, evidence below), 1 survivor
out-of-diff, 1 timeout (caught by the suite), floor=80 / target=100, since=master,
exclusions applied: NONE (raw numbers reported; no code excluded to raise the score — XIX.2).`

The strict floor (80%) is missed by 0.9pp *solely* because 7 of the 43 tested mutants are
provably unobservable (Section 3). No exclusion was applied to arrive at these numbers; the
exception is this documented analysis, which is the only way a vacuous mutant can be closed
without a test (a test that kills an unobservable mutant cannot exist). Per XIX.3 these are
surfaced to the user, not papered over.

## Run parameters

- Command (run from `tests/Management.Tests`):
  `dotnet stryker --since:master --test-case-filter "FullyQualifiedName~TreeLayout|FullyQualifiedName~OrgChartLayout" --break-on-initial-test-failure --skip-version-check`
  (+ `--reporter=json` for the machine-readable run; both runs reproduced: **6.88% headline both times**)
- Stryker.NET 5.1.0 (local dotnet tool, `dotnet-tools.json` pinned)
- Diff scope (`git diff master...HEAD -- src/`): `src/Modules/Management/Application/TreeLayoutService.cs` only
- Environment (Principle XVII / XIX.4): **disposable DB: not required** — the changed code
  path is pure (no EF context); the filtered test set is DB-free.
- Baseline check: filtered suite green before mutation (subset of the 122/122 run).

## Raw numbers (Stryker headline)

- Headline mutation score: **6.88%** (diluted project-wide: 596 of 639 mutants skipped —
  466 NoCoverage (other Management files, out of diff scope), 94 Ignored (already-covered
  block filter), 36 CompileError (killed by the compiler)).
- **Diff scope (the gate's actual population): 43 mutants tested → 34 killed, 8 survived,
  1 timeout (caught).**
  - Strict: 34/43 = **79.1%**
  - Timeout-as-kill (Stryker convention): 35/43 = **81.4%**
- Runtime: **36 s** (2nd run with JSON reporter: 31 s). The 36-s CompileError batch and the
  43-test batch ran in one pass.

## Surviving mutants — full list and per-mutant analysis

| # | Location | Mutant | Status | Classification |
|---|----------|--------|--------|----------------|
| M1 | TreeLayoutService.cs:62 | `g.First()` → `g.FirstOrDefault` (dedup) | Survived | **Vacuous** — a `GroupBy` group is never empty, so `First() == FirstOrDefault()` for every input; the spec's duplicate-Id requirement (termination only) holds identically under both. |
| M2 | TreeLayoutService.cs:84 | precomputed `SubtreeWidth(root);` call → `;` | Survived | **Vacuous** — `SubtreeWidth` is a pure, stateless function (recomputed on demand in `PlaceSubtree`/forest loop); the precompute loop has no side effects. (Code-quality observation: the precompute loop is redundant; recorded, not "fixed for score" per XIX.2.) |
| M3 | TreeLayoutService.cs:96 | `Count > 0 ? MinX : 0.0` → always-true | Survived | **Vacuous** — when `treeRoots` is empty the output list is empty and `min` is never consumed; when non-empty this *is* the original branch. |
| M4 | TreeLayoutService.cs:96 | same → always-false | Survived | **Vacuous** — when non-empty, `min` is structurally 0 (first frame starts at 0 and a leaf sits at the frame start), so `0.0 == MinX`; when empty, `min` is never consumed. |
| M5 | TreeLayoutService.cs:96 | `Count > 0` → `Count >= 0` | Survived | **Vacuous** — same argument as M3/M4: the differing branch value (`double.MaxValue` vs `0.0`) is only produced when the output is empty and `min` is never consumed. |
| M6 | TreeLayoutService.cs:115 | `node.X < min` → `node.X <= min` (in `MinXOf`) | Survived | **Vacuous** — `<=` fires additionally only when `node.X == min`, assigning `min` to itself. |
| M7 | TreeLayoutService.cs:178 | `node.X - min` → `node.X + min` | Survived | **Vacuous by construction** — `min` is structurally always 0 (L6 normalization is a no-op for this placement), so `x + 0 == x - 0` for every input. (L6 is retained because the spec mandates it; the mutant is unobservable, not a spec gap.) |
| M8 | Organization.cs:13 (NOT in diff) | `Name` default `string.Empty` → `"Stryker was here!"` | Survived | **Vacuous / out-of-diff** — Stryker's `--since` also tests mutants covered by *changed test files* (documented behavior); the fixture always sets `Name` explicitly, and the default value is outside the spec's contract (L1–L6 never reference `Name`). |
| M9 | TreeLayoutService.cs:130 | `… && byId.ContainsKey(c.Id)` → `… || visited.Add(c.Id)` (child filter) | **Timeout (caught)** | The suite's bounded cycle test (5 s budget) caught it: the mutated filter bypasses the visited cut for the in-cycle path → non-termination → test timeout. A timeout is a kill in Stryker semantics. |

No surviving mutant exposes a spec-pinned behavior: every one is either structurally
unobservable (M1–M7) or out of the spec's contract (M8). Nothing was excluded; the raw
diff-scope score stands at 79.1% strict / 81.4% with timeout-as-kill.

## Spec-gap routing (Principle XIX.3) — for the user, not for tests

- **G1** (from the attack stage): an org that is both a root (`ParentId` null) and listed
  in another org's `Children` — the spec never says which identity wins (implementation:
  first encounter in input order; renders exactly once, terminates).
- **G2** (from the attack stage): a DAG with a child shared by two parents — the spec
  presumes a tree and never defines the non-owning parent's width/centering for the cut
  child (implementation: the child is dropped from that parent's width).
- **M2 observation** (not a gap): the `SubtreeWidth` precompute loop is redundant (pure
  function recomputed on demand); keeping it costs nothing, removing it is a code-quality
  choice, not a gate action.

## Gate self-test (Principle XIX.5)

See "Gate self-test" section appended below (the gate configuration is new for this repo,
so the self-test is mandatory before this gate's verdict is trusted).

## Gate self-test result (Principle XIX.5) — 2026-11-21

`GATE SELF-TEST: 3/3 caught` — scratch copy via `git worktree add --detach`, each planted
bug in `TreeLayoutService.cs` only, gate's test set
(`--filter "FullyQualifiedName~TreeLayout|FullyQualifiedName~OrgChartLayout"`, 27 tests):

| Planted bug | Mutation | Gate result |
|---|---|---|
| A — off-by-one | `SiblingGap * (Count − 1)` → `SiblingGap * (Count − 2)` in `SubtreeWidth` | **caught** — 1/27 failed |
| B — wrong operator | `node.X < min` → `node.X > min` in `MinXOf` | **caught** — 17/27 failed |
| C — missing validation | dropped `!c.IsDeleted` from the child filter | **caught** — 2/27 failed |

Scratch copy reverted (worktree removed); no planted bug persists on the branch.
Because the gate's configuration is new for this repo, the 3/3 self-test is the trust
prerequisite for the verdict line above — satisfied.

## Gate policy decision (recorded 2026-11-21)

User decision: **option (a)** — floor stays at 80; `PASS-WITH-DOCUMENTED-EXCEPTION`
(per-mutant vacuity evidence, no exclusions, raw score always reported) is the
gate's codified second pass state. This report's verdict line is the template for
that state.
