# Implementation Plan: Organization Tree Branching in Admin Organizations

**Branch**: `story/036-org-tree-branching` | **Date**: 2026-08-24 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/036-org-tree-branching/spec.md`

## Summary

The Admin > Organizations page already fetches all organizations and builds an in-memory tree
(`List<OrgTreeNode>` in `IndexModel`), but the view renders it flat: every non-root node gets the
same 20px `margin-left`, with no branch lines and only a tiny "Root" badge — so parent/child
relationships are invisible. This slice re-renders the page as a true hierarchical tree:
depth-based indentation, CSS-drawn parent→child connector lines, a clearly distinguished root,
visually grouped siblings, and a muted disabled-state treatment — all server-rendered Razor +
pure CSS using the existing Organic design tokens, with no new client-side dependencies and no
changes to the Management module or database.

## Technical Context

**Language/Version**: C# on .NET 10 (GA/LTS), pinned via `global.json`

**Primary Dependencies**: ASP.NET Core Razor Pages (existing), existing `OrganizationService` (Management module) via dependency injection; no new packages

**Storage**: MSSQL via EF Core (`ManagementDbContext`) — **no schema changes**; data already available. View model gains an in-memory `IsDisabled` flag sourced from the existing entity

**Testing**: Playwright E2E (`tests/Playwright.Tests`, existing `06-admin-organizations.spec.ts` extended), `dotnet test` (module unit test projects unchanged), `tests/ArchitectureTests` must keep passing

**Target Platform**: Web (server-rendered HTML/CSS, no new JS); responsive from 375px to desktop

**Project Type**: Web application (modular monolith; view-layer change in `src/Host`)

**Performance Goals**: Page renders instantly for the expected org count (tens to low hundreds of nodes); no measurable regression vs. current page load

**Constraints**: Organic design-system tokens only (no off-system colors/fonts/radii); no new client-side libraries; no horizontal scrolling at ≥375px; read-only change to data flow (list rendering only)

**Scale/Scope**: Single Razor page + one shared partial + CSS in `site.css`; ~1 screen, no new pages, no API changes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Modular Monolith | ✅ Pass | Change is confined to `src/Host` view layer; `OrganizationService` already consumed via DI. No new modules, no new seams |
| II | Clean Architecture, Applied Simply | ✅ Pass | No new abstractions. View model (`OrgTreeNode`) extended with one flag; `DbContext`/service layer untouched |
| III | Module Boundaries Are Compiled | ✅ Pass | No new cross-module references. ArchitectureTests unaffected; must still pass at implement time |
| IV | Human-Legible AI-Authored Code | ✅ Pass (no ADR) | View-layer UX change, not a structural decision (no new boundary, storage choice, or sandboxing change). Rendering approach is documented in `research.md`; no ADR required |
| V | The Sandbox Is Not Optional | ✅ Pass | All work inside `.devcontainer`; touches only repo files |
| VI | Polyglot Storage With a Reason | ✅ Pass | No storage changes — display-only feature |
| VII | Spec-Driven, Sliced Thin | ✅ Pass | Spec 036 exists; vertical slice = one user-visible capability (readable org tree) |
| VIII | Branching Discipline | ✅ Pass (deferred) | `story/036-org-tree-branching` created from `master` at implementation start, before any code edit |
| IX | Plan On Master Only | ✅ Pass | Plan authored on `master` (verified at command start) |
| X | No Ad-Hoc Fixes | ✅ Pass | Issue documented in spec 036 with root cause (flat `margin-left` in `_OrgNode.cshtml`) before any code change |
| XI | Parallel Implementation With Subagents | ➖ N/A at plan time | `[P]` markers handled in `/speckit.tasks`; CSS + E2E-test work will be independent |
| XII | Return to Master After Implementation | ➖ N/A at plan time | Enforced at implement/merge time |
| XIII | Verification Before Claim | ✅ Planned | Build + restart, Playwright E2E (new tree assertions in `06-admin-organizations.spec.ts`), post-merge regression — gates encoded in `quickstart.md` |

**Gate result: PASS — no violations, no Complexity Tracking entries required.**

**Post-Phase 1 re-check: PASS.** Design decisions (R1–R7) introduced no new violations:
CSS-only server-rendered tree (Principle II minimalism), page-level view-model extension only
(no module/service/contract changes — Principles II/III intact), no ADR required (view-layer UX,
not a structural decision — Principle IV), and the three Principle XIII verification gates are
encoded in `quickstart.md`.

## Project Structure

### Documentation (this feature)

```text
specs/036-org-tree-branching/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (rendering approach decisions)
├── data-model.md        # Phase 1 output (Organization entity + OrgTreeNode view model)
├── quickstart.md        # Phase 1 output (validation/run guide)
├── contracts/
│   └── organization-tree-ui.md   # Phase 1 output (UI/DOM contract for the tree)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Host/
├── Pages/
│   ├── Admin/Organizations/
│   │   ├── Index.cshtml          # MODIFIED: tree container markup (semantic nested list, tree region)
│   │   └── Index.cshtml.cs       # MODIFIED (minor): OrgTreeNode record gains IsDisabled flag
│   └── Shared/
│       └── _OrgNode.cshtml       # REWRITTEN: nested-list node with connector hooks, root + disabled markers
└── wwwroot/
    └── css/
        └── site.css              # MODIFIED: .org-tree connector/depth styles + disabled/root styles (Organic tokens)

tests/
└── Playwright.Tests/
    └── tests/
        └── 06-admin-organizations.spec.ts   # EXTENDED: tree structure, indentation, root, disabled, responsive assertions
```

**Structure Decision**: Existing single web-application layout (Option 2 shape: `src/Host` as the
app). No new projects, folders, or client-side assets — the feature is a view-layer change to the
existing Razor page + shared partial + global stylesheet, plus E2E test extensions.

## Complexity Tracking

> No Constitution Check violations — nothing to justify.
