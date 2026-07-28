<!--
  Sync Impact Report
  ==================
  Version change: 1.0.0 → 1.1.0 (MINOR: new principle added)
  Added principles:
    - VIII. Branching Discipline (new)
  Modified principles: none
  Removed sections: none
  Templates updated:
    - ✅ .specify/templates/plan-template.md (branch naming convention)
    - ✅ .specify/templates/spec-template.md (branch naming convention)
    - ⚠  .specify/templates/tasks-template.md (no branch-specific task types needed)
    - ⚠  README.md (no workflow change — branches are agent-side, not documented here)
  Deferred items: none
-->

# Learning LMS Constitution

This project is a teaching exercise, not a product. Its purpose is to learn (a) spec-driven
development with spec-kit and (b) how to sandbox and containerize code written by an AI coding
agent. Every principle below exists to serve one or both of those goals — if a rule doesn't serve
either, it doesn't belong here.

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
Every `/speckit.implement` execution MUST create a dedicated Git branch from `main` so that
agentic coding tasks can run in parallel without interfering with each other or the integration
branch. Branch names follow a strict convention:

- **Prefix**: `bug/` for defect work, `story/` for feature or enhancement work.
- **Format**: `<prefix>/<task-id>-<short-description>` where `task-id` is the numeric or
  alphanumeric identifier from the spec (e.g. `001`, `FEAT-42`) and `short-description` is a
  concise kebab-case phrase (e.g. `story/001-course-catalog-browse`).
- **Lifecycle**: The branch is created at the start of `/speckit.implement`, work is committed
  there, and the branch is merged into `main` (or opened as a PR) only after the implementation
  completes and all validation checks pass. No commits land on `main` outside of a merge.

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
- Any decision that took real discussion to reach (a technology choice, a boundary placement, the
  sandboxing model) gets a short ADR under `docs/adr/`, numbered sequentially.

## Governance

This constitution supersedes ad hoc choices made mid-slice. It is also the document the coding
agent (Pi Agent CLI + Qwen) is expected to read before running `/speckit.plan` or
`/speckit.implement` — if an instruction here is ambiguous to a 27B local model, the fix is to
simplify the instruction, not to add more words explaining it. Amendments require updating this
file, bumping the version below, and — if the amendment reverses a prior ADR — recording that
reversal as a new ADR rather than editing the old one.

**Version**: 1.1.0 | **Ratified**: 2026-07-28 | **Last Amended**: 2026-07-28
