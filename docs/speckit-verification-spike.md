# Spec Kit Verification-First Workflow — Spike Report (spec 059 pilot)

**Branch**: `spike/speckit-verification` (10 commits on master; nothing pushed, no PR — ground rules)
**Date**: 2026-11-21 · **Pilot**: spec 059 — org chart layout collision-free positioning (Management module, `TreeLayoutService`)
**Workflow under test**: Spec Kit 1.4.3 (pi integration) + custom stages `oracle` → `implement` → `attack` → `mutate`, constitution v1.10.0 (Principles XVIII/XIX), gate self-test per XIX.5.

## Verdict

**FEASIBLE WITH CHANGES.** All three pillars ran end-to-end on a real, provably-buggy
feature and produced exactly the evidence they were designed to produce. The changes
required are small and mostly process-level; two are genuine constraints (mutation
threshold for vacuous mutants; subagent reliability in this environment). Nothing in
the stack is infeasible — every gate has a working mechanical form (below).

## What worked

- **Pillar 1 (independent oracles) delivered a real spec defect.** The oracle caught an
  arithmetic error in a *confirmed* spec value (row 9: 134858 → 134865) and a fixture
  contract flaw (L4 requires wiring both sides of each edge). Per Principle XVIII the fix
  went spec → oracle; the implementer never touched a test file. The integrity script
  (`scripts/check-test-integrity.sh`, `--diff-filter=DM` against the `oracle/` tag)
  verified this mechanically — and actually caught the one legitimate oracle re-edit,
  which was resolved by re-tag (the designed path).
- **Pillar 3 (red team) found a real bug the oracles missed.** Attack break B1: the
  spec's per-node L3 rounding vs the implementation's deferred rounding — a nested
  fractional-midpoint shape (expected 507, got 506) that no acceptance example covered.
  The red team also produced 13 permanent regression tests, 12 passing angles, and 2
  spec gaps (G1 root-also-child identity priority; G2 shared-child DAG width) routed to
  the user per XIX.3 instead of being papered over with tests.
- **Pillar 2 (mutation) ran in 36 s on the pilot diff** (`--since:master`) and the
  surviving-mutant report was immediately actionable: 8 survivors, each classified with
  per-mutant evidence (7 structurally vacuous in-diff, 1 out-of-diff, +1 timeout caught
  by the suite's bounded-cycle test). The gate self-test caught **3/3** planted bugs
  (off-by-one, wrong operator, missing validation) on a scratch worktree.
- **Spec Kit infrastructure slots in cleanly**: the repo already had Spec Kit + mandatory
  `extensions.yml` hooks; the three custom stages landed as `.pi/prompts/speckit.*.md`
  + two hook registrations (before_implement → oracle; after_implement → attack, mutate),
  reusing the existing enforcement mechanism. Constitution articles XVIII/XIX fit the
  repo's amendment format (sync-impact report, version bump to 1.10.0).
- **The pilot choice paid off**: a real user-visible bug (overlapping org-chart nodes),
  pure service (no disposable DB needed for the mutation gate), 158-line diff, and a
  clean before/after metric baseline.

## What broke (and what needed manual help)

1. **Repo baseline was RED at HEAD before the spike started**: `dotnet restore` failed —
   the `System.Security.Cryptography.Xml 9.0.20` pin (dated 2026-09-14) carries
   high-severity advisories disclosed afterwards, and the repo's own
   `WarningsAsErrors=NU1903` policy promotes it to an error. *Manual help: a 2-line pin
   bump to 11.0.0 (GA, framework-provided on net10.0 — no runtime behavior change),
   user-approved.* Any team rollout starts with this fix.
2. **Spec Kit 0.14.2 → 1.4.3 upgrade**: the documented `--ai` flag no longer exists
   (`--integration`), the first `init --here --force` run **hangs on an interactive
   prompt** (must add `--non-interactive`), and the upgrade **overwrites the
   constitution** (documented issue; the documented workaround — back up + `git restore`
   — worked exactly as written). Custom files survived (`extensions.yml`,
   `speckit.taskstoissues.md`).
3. **Subagent reliability (environment-specific, not workflow)**: the local 27B model
   used for subagents completed the *read* phase but ended its turn silently before
   acting, twice; a resume with an explicit action-first prompt worked. The oracle and
   attack stages therefore required 2–3 resume nudges. In an environment with a
   competent coding subagent this friction disappears; here it was the single biggest
   manual-effort item. *Residual honesty note: the implement stage was executed by the
   orchestrating session (which had seen the code during discovery), not a fresh
   subagent — the test-side separation (what matters for the oracles' value) held via
   genuine fresh subagent contexts + the mechanical integrity check; the
   implementer-side separation is enforced by the integrity script, not by session
   isolation, in this single-orchestrator setup.*
4. **`specify` CLI not present locally** — the upgrade ran one-shot via
   `uvx --from git+...@v1.4.3` (no global install, per ground rules).
5. Minor: `check-test-integrity.sh` cd path bug (caught by self-test); Stryker
   `--reporter=console` is not a valid 5.1.0 reporter value (use `--reporter=json`);
   E2E suite flakiness on the shared-DB setup (2 flaky + 2 SCORM failures in the
   discovery run; the mutation gate deliberately stays on the unit tier).

## Metrics

| Metric | Value |
|---|---|
| Tests added | **35** (22 oracle: 15 visible + 7 holdout; 13 attack) — `Management.Tests` 87 → **122** |
| Visible suite, pre-implementation | **8 pass / 7 fail** (of 15; 2 more crash the host: row-10 cycle stack overflow, duplicate-Id `ArgumentException`) |
| Holdout suite, pre-implementation | **4 pass / 2 fail + 1 host crash** (of 7) |
| Visible + holdout, post-implementation | **22 / 22 pass** |
| Full `Management.Tests`, post-implementation | **122 / 122 pass** (zero regressions in the 87 pre-existing tests) |
| Mutation score, pre-fix | **N/A by gate design** — `--break-on-initial-test-failure` aborts a gate on a red baseline (the pre-fix evidence is the red state above) |
| Mutation score, post-fix (diff scope, 43 mutants) | **79.1% strict / 81.4% with timeout-as-kill** (Stryker headline 6.88%, diluted by 596 skipped project-wide mutants) |
| Surviving mutants | **8** — 7 in-diff, all structurally vacuous with per-mutant evidence (M1–M7, see `specs/059-org-chart-layout/mutation-report.md`); 1 out-of-diff (`Organization.cs` `Name` default — outside the spec's contract); **no exclusions applied**; gate verdict `PASS-WITH-DOCUMENTED-EXCEPTION` (floor 80 missed by 0.9pp solely due to vacuous mutants) |
| Timeout (caught) | 1 — logical mutation in the child filter bypassed the visited cut → non-termination caught by the bounded cycle test |
| Attack findings | **1 confirmed break (B1 — fixed), 2 spec gaps (G1, G2 — routed to user), 12 passing angles (13 regression tests)** |
| Planted bugs caught (gate self-test) | **3 / 3** (off-by-one: 1 test failed; wrong operator: 17 failed; missing validation: 2 failed) |
| Runtimes | Stryker pilot-diff run **31–36 s**; filtered test set (27 tests) **0.5 s**; full `Management.Tests` (122) **0.5 s**; full 5-project unit suite (220 tests) **~40 s** warm; E2E (184 pass/2 fail/2 flaky) **4.0 min**; warm build **~30 s**; host start (migrations + seeders) **~6 s**; oracle stage agent wall-time **~15 min** (incl. resume nudges) |

## Effort estimate & blockers for team rollout

**One-off setup: ~1 day.** (1) the NU1903 pin fix (done, 2 lines); (2) Spec Kit
upgrade to ≥1.4.3 (done; 15 min incl. constitution restore); (3) constitution
XVIII/XIX + spec-template sections (done; already in the spike branch); (4) the three
command prompts + hook registrations + `check-test-integrity.sh` (done); (5) `dotnet-tools.json`
pinning `dotnet-stryker` (done); (6) branch protection + CODEOWNERS — **applied 2026-11-21**: `master` requires `build-and-test` + `verification-gates` (admin bypass enabled, so the documented-exception route stays executable); CODEOWNERS routes `tests/` and the constitution to the gate owner (review routing only — enforcement is the status checks).

**Per-feature steady state: ~0.5–1 h of gate time** for a pilot-sized diff (Stryker
36 s + test runs ~1 min + agent wall-time for the oracle/attack passes, dominated by
model quality). Whole-module runs: 1–3 h.

**Blockers to address before full rollout:**

1. **Subagent capability** — the oracle/attack stages need a subagent that can execute
   a multi-file write + run loop in one turn. This environment's local 27B model
   needed resume nudges (worked, but added ~15 min of manual supervision). On a
   stronger model (or with the nudges scripted into the prompts' "if you finish the
   read phase, your next action is a write call" wording) the friction largely
   disappears. This is the #1 rollout dependency.
2. **Vacuous mutants and the 80% floor — DECIDED 2026-11-21 (user): option (a)** —
   keep the floor at 80 and codify **PASS-WITH-DOCUMENTED-EXCEPTION** (per-mutant
   vacuity evidence in the spec's mutation report, no exclusions, raw score always
   reported) as the gate's second pass state. Option (c) — `since.ignore-changes-in`
   to dodge the floor — remains a XIX.2 violation in all cases.
3. **Shared-DB unit suites** — the mutation gate must be the only writer during its
   run and (for non-pure features) use a disposable database (Principle XVII). The
   repo's existing `sequential-project-test-runner` hook already solves the sequential
   side; the disposable-DB side is a 10-line addition to the mutate gate (create →
   point `ConnectionStrings__Sql` → drop) — **present in the merged
   `speckit.mutate.md` step 2 (throwaway DB, only-writer rule, drop afterwards).
   Disposition decision (blocker 2) on the vacuous-mutant exception flow.
4. **E2E flakiness** is orthogonal (unit tier carries the mutation gate). Characterized
   by a clean-DB re-run (2026-11-21, Agent B): DB dropped and recreated via
   migrations + seeders (verified pre-suite: 10 courses / 1 seed enrollment / 0
   attempts / 1 SCORM package), then the full suite with `CI=1`, 1 worker, 2 retries:
   **188 passed / 0 failed / 2 flaky / 1 skipped / 0 did-not-run**. The 2 SCORM specs
   that failed in the dirty discovery run (`15-scorm-launch-ui`, `20-scorm-session-authz`)
   **pass on a clean DB first try** — the dirty-run failures were state-coupling (stale
   unit-written SCORM sessions / Valkey state), not bugs. The 2 flaky specs
   (`03-enrollment:21`, `14-profile-courses:92` — `toBeVisible` 10 s timeouts that pass
   on retry) fail first-try in **both** clean and dirty runs: timing flakes, and the
   same set in both states rules out DB-state as the cause. Caveats: two earlier
   "clean-DB" runs were invalidated by a local Valkey environment confound (container-
   internal hostnames unresolvable from the Windows host — the specs' FLUSHALL helper
   defaults to `valkey:6379`); and the dirty-DB direction was not re-confirmed by the
   re-run. Local-run portability note: E2E on a dev machine requires exporting
   `ConnectionStrings__Valkey` for both app and test processes.
5. **Spec gaps accumulate** — G1/G2 from the pilot show the spec template needs the
   "which identity wins / shared child" style of clause for anything involving
   references between entities. **G1/G2 themselves resolved 2026-11-21 (PR #13,
   user decisions): G1 = root-identity wins (L4 clause + code change, 3-cycle
   attack test re-pinned); G2 = input-contract violation with the duplicate-Id
   treatment (no code change).** The template fix (one line in Key Entities)
   remains to be applied before the next spec that references entities.

## Recommended CI changes (APPLIED 2026-11-21, PR #16)

Applied with the v1 scoping decisions: the `verification-gates` job runs only on
PRs touching `tests/` (scope step), enforces the 80% floor with timeout-as-kill
(hard floor; PASS-WITH-DOCUMENTED-EXCEPTION is the maintainer-override route —
executable while no required-check bypass exists), `Management.Tests` scope with
the documented per-project extension path, and a loud-fail parse guard on a red
baseline (validated against Stryker 5.1.0, pinned rollForward:false). Second-agent
review (Agent B) findings: multi-commit pushes check only HEAD~1..HEAD (PR gate
is primary); FsCheck fresh-seed flake is a low-probability loud-fail risk;
CompileError/runtime-Error mutants are excluded from the K/S/T denominator
(Stryker-standard); the no-oracle-tag fail-loud path is reachable on PRs
(fetch-depth: 0 fetches tags). Branch protection + CODEOWNERS below remain
unapplied (repository settings, owner decision).

Original recommendation follows.

Add to `.github/workflows/ci.yml` (new job `verification-gates`, runs on PR, after
the existing `build-and-test` job's unit-test steps — or as an extension of it):

```yaml
  # Verification gate stack (constitution XVIII/XIX) — spike 059, 2026-11-21.
  verification-gates:
    runs-on: ubuntu-latest
    needs: build-and-test
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }            # --since needs the base history
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: "10.0.x" }
      - name: Set up local dotnet tools
        run: dotnet tool restore
      - name: Test integrity (XVIII.2 — no test edits after the oracle tag)
        run: |
          if [ -z "$(git tag -l 'oracle/*' | head -1)" ]; then
            # No oracle tag on the PR branch: the PR either predates the workflow
            # or skipped the oracle stage. Fail loudly (XVII.2: a step that
            # cannot fail is not evidence).
            echo "::error::No oracle/* tag — the oracle stage (XVIII) has not run for this feature."
            exit 1
          fi
          bash scripts/check-test-integrity.sh
      - name: Mutation gate on the PR diff (XIX.2)
        working-directory: tests/Management.Tests   # one step per affected test project
        run: |
          # --break-on-initial-test-failure: a red baseline aborts (proves nothing).
          # No since.ignore-changes-in is ever set (XIX.2 shrunken-surface rule).
          dotnet stryker --since:${{ github.event.pull_request.base.sha }} \
            --test-case-filter "FullyQualifiedName~<affected module>" \
            --break-on-initial-test-failure --skip-version-check --reporter=json
          # Gate: diff-scope score >= 80 (floor) with PASS-WITH-DOCUMENTED-EXCEPTION
          # allowed only when every surviving mutant carries vacuity evidence in the
          # spec's mutation-report.md (parsed from the JSON report).
```

Plus (repository settings, not pipeline):
- **Branch protection on `master`**: require the `verification-gates` + existing
  `build-and-test` status checks; no direct pushes. (Currently: no branch protection.)
- **CODEOWNERS** (new file):
  ```
  /tests/   @<reviewer-or-oracle-owner>
  /.specify/memory/constitution.md  @<reviewer>
  ```
  (CODEOWNERS routes review; the *enforcement* is the status check above.)
- Disposition decision (blocker 2) on the vacuous-mutant exception flow.

## Exact commands to reproduce the spike

```bash
# 0) Baseline fix (done in commit 3880a4a): Directory.Build.props pin 9.0.20 -> 11.0.0
# 1) Spec Kit upgrade (one-shot, no global install)
cd LibreLMS
cp .specify/memory/constitution.md /tmp/constitution-backup.md
uvx --from "git+https://github.com/github/spec-kit.git@v1.4.3" \
  specify init --here --force --non-interactive --integration pi
git restore .specify/memory/constitution.md        # documented workaround
# 2) Tooling
dotnet new tool-manifest
dotnet tool install dotnet-stryker --version 5.1.0
# FsCheck 3.6.0 + FsCheck.Xunit 3.6.0 added to tests/Management.Tests (dev-deps)
# 3) Infrastructure (commits e5f2b25 + 6ae4551): constitution XVIII/XIX, spec template,
#    .pi/prompts/speckit.{oracle,attack,mutate}.md, extensions.yml hooks,
#    scripts/check-test-integrity.sh
# 4) Spec + user confirmation (commits 5760e77, 542b205)
# 5) ORACLE — fresh subagent context; reads ONLY spec.md + plan's Public Interface
#    (+ Organization.cs/Entity.cs type shapes). Writes OrgChartLayoutTests.cs +
#    Holdout/OrgChartLayoutHoldoutTests.cs, records red state, commits + tags:
git commit -m 'oracle(059): ...'; git tag oracle/059-org-chart-layout
#    red state: dotnet test tests/Management.Tests/Management.Tests.csproj \
#      --filter "FullyQualifiedName~OrgChartLayout"   # 12 pass / 8 fail (+2 host crashes)
# 6) IMPLEMENT — spec L1-L6 rewrite of TreeLayoutService.cs (commit 8fee7cd);
#    oracle re-edit of the spec-corrected constant (8ee16c0); git tag -f oracle/...
# 7) ATTACK — fresh subagent; may read implementation + spec; writes tests/**/Attack/*:
dotnet test tests/Management.Tests/Management.Tests.csproj   # 122/122 after B1 fix
# 8) MUTATE — from the test project directory:
cd tests/Management.Tests
dotnet stryker --since:master \
  --test-case-filter "FullyQualifiedName~TreeLayout|FullyQualifiedName~OrgChartLayout" \
  --break-on-initial-test-failure --skip-version-check --reporter=json
#    -> 43 tested: 34 killed / 8 survived (all classified) / 1 timeout caught; 36 s
# 9) GATE SELF-TEST (XIX.5) — scratch worktree, 3 planted bugs, gate test set:
git worktree add --detach ../librelms-selftest-059 HEAD
#    plant A: (Count - 1) -> (Count - 2)  -> 1/27 failed (caught)
#    plant B: node.X < min -> >           -> 17/27 failed (caught)
#    plant C: drop !c.IsDeleted          -> 2/27 failed (caught)
git worktree remove --force ../librelms-selftest-059
# 10) Integrity gate at every stage:
bash scripts/check-test-integrity.sh
```
