# Implementation Plan: Interactive Organization Chart View

**Branch**: `story/013-org-chart-view` | **Date**: 2025-08-01 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/013-org-chart-view/spec.md`

## Summary

Replace the current indented card-list organization view (`/Admin/Organizations/Index`) with an interactive, zoomable org chart rendered as a top-down hierarchical tree. Admins (SuperUser, OrgAdmin) can right-click any organization node for a context menu offering: edit org, disable/enable org, add new user, assign existing user, and assign course. The chart auto-fits on load and supports pan/zoom navigation for large hierarchies. Dynamic updates via HTMX avoid full page reloads.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (GA)

**Primary Dependencies**: ASP.NET Core minimal APIs, Razor Pages, EF Core, HTMX 2.0.4 (already loaded), StackExchange.Redis (Valkey)

**Storage**: MSSQL via EF Core — `Organizations` table already exists in `ManagementDbContext`. No new tables required; feature is UI-driven using existing domain models and services.

**Testing**: xUnit (existing `ArchitectureTests` project via NetArchTest). Unit tests for new layout logic and service extensions; integration tests for new API endpoints.

**Target Platform**: Web browser (desktop-first). Razor Pages served by ASP.NET Core on Linux (inside devcontainer).

**Project Type**: Web application — management portal within the Libre LMS modular monolith.

**Performance Goals**: Chart renders within 1 second for hierarchies up to 100 org nodes. Zoom/pan responds within 200ms. Context menu appears within 150ms.

**Constraints**: No new JavaScript frameworks. Leverage existing HTMX patterns. Keep inline CSS approach consistent with project. No network seams between modules beyond existing `Management.Contracts` boundary.

**Scale/Scope**: Admin-facing feature. Expected org hierarchies: 5–100 nodes. 1–3 concurrent admin users.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | All changes live within Host (Razor Pages + JS) and Management module. No cross-module boundary violations. |
| II. Clean Architecture | ✅ PASS | New service methods added to `OrganizationService` (Application layer). No infrastructure leakage into Domain. |
| III. Module Boundaries Compiled | ✅ PASS | No new cross-module references. Management module contracts unchanged. |
| IV. Human-Legible Code | ✅ PASS | Tree layout and pan/zoom will be documented with inline comments and an ADR for the SVG rendering approach. |
| V. Sandbox Not Optional | ✅ PASS | Development inside devcontainer; no host filesystem access. |
| VI. Polyglot Storage | ✅ PASS | No new storage. Uses existing MSSQL `Organizations` table. No Valkey changes needed. |
| VII. Spec-Driven, Sliced Thin | ✅ PASS | Single vertical slice: org chart UI with full CRUD + assign actions. |
| VIII. Branching Discipline | ✅ PASS | Branch: `story/013-org-chart-view`. |
| IX. Plan On Master | ✅ PASS | Planning on `master` branch. |
| X. No Ad-Hoc Fixes | ✅ PASS | Spec exists at `specs/013-org-chart-view/spec.md`. |

## Project Structure

### Documentation (this feature)

```text
specs/013-org-chart-view/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code Changes

```text
src/
├── Host/
│   ├── Pages/Admin/Organizations/
│   │   ├── Chart.cshtml          # New: org chart page (replaces Index as primary view)
│   │   └── Chart.cshtml.cs       # New: page model for org chart
│   ├── Pages/Shared/
│   │   └── _OrgContextMenu.cshtml # New: partial for context menu rendering
│   └── wwwroot/
│       └── js/
│           └── org-chart.js       # New: pan/zoom + context menu + HTMX wiring
├── Modules/Management/
│   ├── Application/
│   │   └── OrganizationService.cs # Modified: add GetChartTreeAsync, DisableAsync, EnableAsync
│   └── Endpoints/
│       └── OrganizationEndpoints.cs # Modified: new DTOs for chart data (OrgChartNodeDto, etc.)
└── SharedKernel/
    └── (no changes expected)
```

**Structure Decision**: All UI changes live in `Host/Pages` and `Host/wwwroot/js` following the existing Razor Pages + HTMX pattern. Backend changes are scoped to `Management/Application` and `Management/Endpoints` — no new modules created. This aligns with Principle I (modular monolith) and Principle VII (thin slices).

## Phase 0: Research

See [research.md](research.md) for resolved technical decisions.

## Phase 1: Design & Contracts

See [data-model.md](data-model.md), [contracts/](contracts/), and [quickstart.md](quickstart.md).

## Complexity Tracking

No constitution violations to justify. All principles satisfied.
