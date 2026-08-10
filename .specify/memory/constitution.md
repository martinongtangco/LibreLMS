<!--
  Sync Impact Report
  ==================
  Version change: 1.7.0 → 1.7.1 (PATCH: strengthened Principle XIII)
  Added sections: none
  Modified principles:
    - XIII. Verification Before Claim — added concrete proof requirements for each gate;
      agent must show build output, test results, and restart evidence — not just claim
      a gate is passed
  Removed sections: none
  Templates updated:
    - .specify/templates/tasks-template.md — ✅ no changes needed
    - .specify/templates/plan-template.md — ✅ no changes needed
    - .specify/templates/spec-template.md — ✅ no changes needed
    - README.md — ✅ no changes needed (references constitution as source of truth)
    - AGENTS.md — ✅ no changes needed (references constitution as source of truth)
  Deferred items: none
-->

# Learning LMS Constitution

This project is a teaching exercise, not a product. Its purpose is to learn (a) spec-driven
development with spec-kit and (b) how to sandbox and containerize code written by an AI coding
agent. Every principle below exists to serve one or both of those goals — if a rule doesn't serve
either, it doesn't belong here.

## ⚠️ Before You Touch Code

Before editing ANY file, you MUST do the following. No exceptions.

1. **Check your branch**: Run `git branch --show-current`. You must be on a `bug/` or `story/` branch. If you are on `main`, `master`, or any other branch, STOP and create the correct branch first.
2. **Check for a spec**: A spec must exist for this change. If it doesn't, create one via `/speckit.specify` on `master` before branching and coding.
3. **Declare before editing**: Before every file edit, state:
   - The branch you are on
   - The spec or issue this change belongs to
   - Which constitution principles apply

If any step fails, STOP. Do not proceed to edit files.

## Core Principles

### I. Modular Monolith, Not Microservices (Yet)
The system ships as one deployable ASP.NET Core process (`Host`) composed of independent modules
(`Catalog`, `Enrollment`, `Scorm`). This gets the organizational benefit of module boundaries
without paying the operational cost of network calls, service discovery, or distributed
transactions before there's a real reason to. Each module's `*.Contracts` project is a rehearsal
for what would become a network API boundary if a module ever needed to split out into its own
service — the seam is designed in from day one, even though it isn't a network seam yet.

### II. Clean Architecture, Applied Simply
Inside each module, dependencies point inward: `Domain` knows nothing about `Application`,
`Application` knows nothing about `Infrastructure` or `Endpoints`. That's the whole rule. Do not
add MediatR, a CQRS framework, or a repository layer wrapping EF Core's `DbContext` unless a
specific, current problem requires it — `DbContext` already *is* the repository/unit-of-work
abstraction; wrapping it again just adds a layer a human has to read through for no behavioral
gain. Every abstraction that *does* get introduced must be explainable in one plain sentence to
someone who knows C# but not this codebase. If it can't be, simplify it.

### III. Module Boundaries Are Compiled, Not Conventional
A module may only be referenced by other modules through its `*.Contracts` project (DTOs and
interfaces). No module project ever references another module's `Domain`, `Application`, or
`Infrastructure` internals directly. This is enforced by an `ArchitectureTests` project
(NetArchTest) that fails the build on violation — the point is that an AI agent (or a human)
*cannot* accidentally cross a module boundary and have it silently compile. A convention that
relies on memory or code review isn't a boundary; a failing build is.

### IV. Human-Legible AI-Authored Code
Every non-obvious structural decision — a module boundary, a storage choice, the sandboxing
approach — gets a short ADR in `docs/adr/` (context → decision → consequences, one page or less).
Code favors explicit, straightforward control flow over clever generalization. The target reader
is someone with solid general C#/.NET knowledge but no prior exposure to this repo or to whichever
framework-of-the-month pattern might otherwise get reached for. If a reviewer has to ask "why did
the agent do it this way," that question should already be answered in an ADR, not just in the
agent's now-gone reasoning.

### V. The Sandbox Is Not Optional
All coding-agent work (Pi Agent CLI driving a local Qwen model) happens inside the
`.devcontainer`. The agent's process only ever touches the repo's bind-mounted files and the
sibling containers defined in `docker-compose.yml` (`mssql`, `valkey`) — never the host filesystem
outside the mount, never arbitrary outbound network. This is the project's core teaching thesis:
an agent that can rewrite its own instructions or run arbitrary shell commands should not also
have an open door to the rest of the machine. If a task seems to require reaching outside the
container, that's a signal to redesign the task, not to loosen the boundary.

### VI. Polyglot Storage With a Reason, Not by Default
MSSQL is the system of record for everything durable — users, courses, enrollments, final
completion status. The Redis-protocol store (Valkey) is used *only* for ephemeral, high-churn
state that doesn't need relational guarantees: specifically, the live SCORM `cmi.*` runtime bag
during an in-progress attempt, which is written on every `LMSSetValue` call and only persisted to
MSSQL on `LMSCommit`/`LMSFinish`. Nothing lives permanently in Valkey that isn't either derived
from or eventually committed to SQL. If a future slice wants to put something else in Valkey, the
question to answer first is "would losing this on a cache flush actually be fine?" — if not, it
belongs in SQL.

### VII. Spec-Driven, Sliced Thin
No code gets written before its slice has gone through `/speckit.specify` → `/speckit.plan` →
`/speckit.tasks` → `/speckit.implement`. Slices are vertical — a whole user-visible capability
(e.g. "browse and enroll in a course") — not horizontal ("build the whole Domain layer for every
module first"). A module only gets built when a slice currently needs it; no module is scaffolded
ahead of demand.

### VIII. Branching Discipline
Every coding task — whether planned or discovered ad-hoc — MUST run on a dedicated Git branch
from `main` so that agentic work can proceed in parallel without interfering with each other or
the integration branch. No commits land on `main` outside of a merge. Branch names follow a
strict convention:

- **Prefix**: `bug/` for defect work, `story/` for feature or enhancement work.
- **Format**: `<prefix>/<task-id>-<short-description>` where `task-id` is the numeric or
  alphanumeric identifier from the spec (e.g. `001`, `FEAT-42`) and `short-description` is a
  concise kebab-case phrase (e.g. `story/001-course-catalog-browse`).
- **Lifecycle**: The branch is created before code changes begin, work is committed there, and
  the branch is merged into `main` (or opened as a PR) only after the implementation completes
  and all validation checks pass.

### IX. Plan On Master Only
Any SpecKit command that performs planning or creates user stories
(`/speckit.specify`, `/speckit.plan`, `/speckit.tasks`) MUST verify the
active Git branch is `master` before proceeding. If the current branch is
anything other than `master` (e.g. a `story/` or `bug/` working branch), the
command MUST stop immediately and report:

- The current branch name
- That planning commands must run on `master`
- The instruction to switch back: `git checkout master`

This prevents specs, plans, and task lists from being authored while already
inside an implementation branch — they belong on the integration branch where
the full project state is visible. Implementation work on feature branches
follows *after* planning is complete on `master`.

### X. No Ad-Hoc Fixes — Document Before You Code
When an issue is discovered through conversation with the AI (outside a planned slice), the fix
MUST still follow the SpecKit workflow — no direct code edits on `main` or any branch without
documentation. The agent MUST NOT fix things "inline" in a chat session.

The required flow for an ad-hoc issue:

1. **Root cause first**: Before touching code, identify and state the root cause of the issue.
2. **Branch off**: Create a `bug/<id>-<desc>` branch from `main` (Principle VIII) before any
   code changes begin.
3. **Document via SpecKit**: Run `/speckit.specify` to capture the issue, root cause, and
   proposed fix in a spec. Then `/speckit.plan` → `/speckit.tasks` → `/speckit.implement` to
   execute the fix through the normal pipeline. This creates a permanent record of the decision
   and the change.
4. **Structural changes get an ADR**: If root cause analysis reveals the issue stems from a
   design or architectural decision, record that finding as an ADR under `docs/adr/` before
   proceeding to the SpecKit steps.

The spec entry serves as the decision record — future interactions can review what was changed
and why. There is no "too small to document" exemption for code changes. Every edit — including
typos, misnamed variables, and single-line fixes — requires a branch and a minimal spec.

### XI. Parallel Implementation With Subagents
When running `/speckit.implement`, the agent MUST use pi subagents to parallelize independent
work items wherever possible. Tasks marked `[P]` in `tasks.md` MUST be dispatched as parallel
subagent runs using the `tasks[]` parallel execution mode. Independent user stories, models
operating on separate files, service implementations touching distinct modules, and test suites
for different endpoints can all proceed concurrently. Chain workflows (`chain[]`) should be used
for sequential dependencies within a single story or task group.

The parent session retains final decision authority — it orchestrates the workflow, synthesizes
subagent results, and applies any integration fixes. Never delegate the final merge step to a
child; the parent is the sole writer for the shared `cwd`.

Rationale: The SpecKit task list already identifies which work is parallelizable with `[P]`
markers. Enforcing subagent parallelism turns that structural information into actual throughput
gains, reducing wall-clock time for slice implementation without sacrificing the quality
constraints enforced by the other principles.

### XII. Return to Master After Implementation
After an implementation slice completes — whether the branch is merged into `master` or opened
as a PR — the agent MUST switch the working branch back to `master` (`git checkout master`).
This is mandatory; the session MUST NOT remain on a `story/` or `bug/` branch after the
implementation session ends.

This closes the loop with Principle IX (Plan On Master Only): the agent returns to `master` so
the next SpecKit command starts from the integration branch where the full project state is
visible. An agent session that stays on a completed feature branch risks running the next
`/speckit.specify` or `/speckit.plan` from the wrong context, or accidentally accumulating
unrelated commits on a finished branch.

### XIII. Verification Before Claim
An agent MUST NOT claim an issue is fixed or a feature is complete until all three verification
gates pass **with concrete proof** from each:

1. **Compiles and runs**: Rebuild the app (`dotnet build`), restart the process, and confirm
   a running process responds to HTTP requests. Show build output and the "Now listening"
   log line — do not claim "it compiles" without running the build.
2. **E2E tests validate the change**: Run Playwright tests against the running application
   and show passing output. If no relevant test exists, the agent MUST write one before
   claiming completion. Show test results — do not claim "tests pass" without running them.
3. **Post-merge regression**: After merging to `master`, rebuild, restart, and re-run
   Playwright tests against the merged code. Show passing output — do not assume the merge
   preserved correctness.

A fix that compiles but has no E2E test is unverified. A test that passes on a feature branch
but isn't re-run after merge is incomplete. Claiming a gate is passed without showing evidence
from running that gate is a violation. All three gates are mandatory — no exemptions.

Rationale: Without automated verification, the agent has no way to distinguish "works on my
machine" from "actually fixed." Playwright tests provide the observable evidence that a change
behaves as intended from the user's perspective, not just that it compiles.

## Technology & Scope Constraints

- **.NET 10 (GA/LTS)**, pinned via `global.json` to a released SDK band — never a preview band,
  even though this is a learning project; the toolchain being stable removes one variable from an
  already-experimental exercise.
- **C#**, ASP.NET Core minimal APIs for module endpoints, EF Core against MSSQL.
- **StackExchange.Redis** client against a **Valkey** server (Redis-protocol-compatible, BSD-3
  licensed) rather than Redis itself, to sidestep Redis's 2024 license change for a project with no
  reason to need Redis Ltd.'s specific licensing terms.
- **Web portal**: Razor Pages or Blazor Server, chosen at `/speckit.plan` time for whichever slice
  needs it first — default to whichever option needs the fewest moving parts for that slice's
  actual requirement, not the "more architecturally interesting" option.
- **SCORM support is deliberately SCORM 1.2, simplified**: manifest parsing, static content
  serving, and a JS API shim covering `LMSInitialize/LMSFinish/LMSGetValue/LMSSetValue/LMSCommit`
  plus the CMI fields real authoring-tool output actually needs to avoid breaking
  (`cmi.core.student_id`, `student_name`, `lesson_status`, `credit`, `entry`, `exit`,
  `score.raw`, `session_time`, and `cmi.suspend_data`). SCORM 2004, multi-SCO sequencing, and
  `cmi.interactions` are explicitly out of scope.

## Development Workflow

- All development happens inside `.devcontainer`; `docker compose up` brings up `mssql` and
  `valkey` as sibling services before any module that needs them is implemented.
- `dotnet test tests/ArchitectureTests` must pass before a slice is considered done — this is the
  automated check for Principle III.
- Playwright E2E tests must pass before any fix or feature is claimed complete (Principle XIII).
  If no test covers the changed behavior, write one.
- Any decision that took real discussion to reach (a technology choice, a boundary placement, the
  sandboxing model) gets a short ADR under `docs/adr/`, numbered sequentially.

## Governance

This constitution supersedes ad hoc choices made mid-slice. It is also the document the coding
agent (Pi Agent CLI + Qwen) is expected to read before running `/speckit.plan` or
`/speckit.implement` — if an instruction here is ambiguous to a 27B local model, the fix is to
simplify the instruction, not to add more words explaining it. Amendments require updating this
file, bumping the version below, and — if the amendment reverses a prior ADR — recording that
reversal as a new ADR rather than editing the old one.

**Version**: 1.7.1 | **Ratified**: 2026-07-28 | **Last Amended**: 2026-08-10
