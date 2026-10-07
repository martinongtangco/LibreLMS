# Handoff: spec 058 — Code Review Remediation → Pi/Qwen agent

**To**: the Pi agent (Qwen model) picking up this work
**From**: Claude Code session, 2026-10-05
**Your worktree**: `C:\SandboxDev\SbxTestWSpecKit\.claude\worktrees\qwen-058-code-review-remediation`
**Your branch**: `story/058-code-review-remediation` (already created, already checked out there)
**Your spec**: `specs/058-code-review-remediation/spec.md` (already written — read it in full before
doing anything else)

## Before you touch any code

Per `AGENTS.md` / the constitution's "⚠️ Before You Touch Code" section, confirm and declare:

1. `git branch --show-current` from your worktree → must print `story/058-code-review-remediation`.
2. The spec at `specs/058-code-review-remediation/spec.md` already exists — you don't need to run
   `/speckit.specify`. You DO still need to run `/speckit.plan` and `/speckit.tasks` on it yourself
   before writing code, per the normal cycle (the spec's Assumptions section says this explicitly).
3. State which constitution principles apply before each edit (this spec touches Principles II,
   III, VI, XIII–XVII at minimum — re-read `.specify/memory/constitution.md` if any are unfamiliar).

## What this spec covers

Four independently-committable milestones, in priority order — see
`specs/058-code-review-remediation/spec.md` for full acceptance criteria:

1. **P1 — Dashboard completion stats are fake.** `DashboardService.cs:67,101,116-117` hardcodes
   `0.0`/`0` for completion rate, completed-course count, and average score on every dashboard. Wire
   these to real SCORM attempt data via `Scorm.Contracts`.
2. **P2 — Management services lack unit tests.** `DashboardService`, `OrganizationLookup`,
   `UserInfoLookup`, `OrgSubtree`, `TreeLayoutService` have zero coverage. Add tests, especially for
   the subtree-aggregation logic Story 1 touches.
3. **P3 — Two competing error-handling conventions.** Exceptions (caught via ~10 repeated 4-block
   catches in `Program.cs`) vs. `SharedKernel.Result<T>` (used only by 2 services). Write an ADR in
   `docs/adr/` (next number after 0013) picking a convention, then apply it to at least the 4 cited
   catch-blocks without changing observable HTTP behavior.
4. **P4 — Housekeeping.** Duplicate `Database.Migrate()` calls (`Program.cs:161-167`), missing
   ArchitectureTests assertions (Contracts-reverse-dependency, SharedKernel isolation), and
   inconsistent auth-group declaration style across `adminEnrollments`/`users`/`orgs`/`adminCourses`.

## Working agreement for this long-running task

- **Commit after every milestone**, not just at the end. This repo's observed commit convention
  (verify yourself in `git log`, don't just trust this line) is
  `feat(058): …` / `fix(058): …` / `test(058): …` / `docs(058): …` — one commit or tightly-scoped
  commit set per milestone, each referencing spec 058.
- **A separate reviewing session (Claude) checks your branch roughly every 30 minutes.** It reviews
  by reading your commits and gate evidence — it does **not** interrupt, push to your branch, or
  edit your worktree. If it finds a problem, it will say so in a note (check
  `specs/HANDOFF-RUN-LOG.md` or wait for direct contact) rather than touching your files. You do not
  need to wait for its go-ahead between milestones — keep moving and let review happen
  asynchronously, per Principle XVI (independent verification happens before *merge*, not before
  every commit).
- **Bounded retry (Principles XIV/XV) still applies per milestone**: max 2 attempts at the same
  failing gate; classify the failure (local/design/blocking) before a 2nd attempt; on a 2nd failure
  write a diagnosis into this spec's task notes and stop that milestone rather than looping.
- **Verification (Principle XIII)** needs pasted evidence for each milestone: `dotnet build` +
  the app running, Playwright green against it, and — after this branch merges to master — a
  rebuild/restart/re-run. State explicitly whether your evidence is host-side or
  `.devcontainer`-side (CLAUDE.md §5 — this is still an open, unresolved conflict; don't silently
  pick a side, just say which mode you ran in).
- **When the whole branch is done**: `git checkout master` (Principle XII) from your worktree,
  and flag it for merge/PR — don't merge it yourself without the independent-verification gate
  (Principle XVI) being satisfied.
- Update `specs/HANDOFF-RUN-LOG.md` with your per-milestone commits and gate evidence, following
  its existing entry format — that log is the shared source of truth both agents read.

## One known deviation already baked into your worktree

The spec commit (`spec(058): …`) landed on a worktree-local branch that mirrors master's tip
(`worktree-cheeky-zooming-possum`) rather than literally on `master`, because the reviewing Claude
session was itself confined to an isolated worktree and couldn't update the shared `master` ref
(it's checked out elsewhere). **Master is one commit behind** until a human or an unconfined session
fast-forwards it. This doesn't block you — your branch already has the spec — but don't be surprised
if `git log master` doesn't show the spec commit yet; it will once that fast-forward happens.
