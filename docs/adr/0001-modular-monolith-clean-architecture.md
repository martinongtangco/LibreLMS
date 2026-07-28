# 0001: Modular Monolith with (simplified) Clean Architecture

## Status
Accepted — 2026-07-28

## Context
This project exists to learn spec-driven development and AI-agent sandboxing, using a small LMS
as the vehicle. We need an architecture that:
- teaches real module boundaries (the "phase before microservices" the user asked to simulate),
- stays simple enough that a human can follow every structural decision an AI coding agent makes,
- doesn't require standing up multiple deployable services, service discovery, or distributed
  transactions before there's any real load or team-scaling reason to.

Three options were considered:
1. **Plain layered monolith** (one `Domain`/`Application`/`Infrastructure` for the whole app,
   feature folders inside). Simplest to build, but doesn't teach or enforce any module boundary —
   everything can reference everything, which is exactly the failure mode a "path to microservices"
   exercise should avoid.
2. **Microservices from day one** (separate deployable API per module, own database per service).
   Teaches the target end-state directly, but the operational cost (network calls, service
   discovery, eventual consistency, N containers to run locally) is disproportionate to a
   three-module teaching app, and would bury the actual lesson (agent sandboxing, spec-driven
   flow) under infrastructure ceremony.
3. **Modular monolith**: one deployable process (`Host`), each module physically separated into
   its own project with a thin `*.Contracts` project as the only allowed cross-module surface.

## Decision
Use a modular monolith: `src/Modules/{Catalog,Enrollment,Scorm}` each as their own `.csproj`
(Domain/Application/Infrastructure/Endpoints as folders inside — not separate projects, to avoid
12+ project files for a 3-module app), plus a `{Module}.Contracts` project per module that is the
*only* thing another module or Host is allowed to reference for cross-module needs.

Inside each module, Clean Architecture's inward-dependency rule applies (Domain has zero
dependencies; Application depends only on Domain and its own abstractions; Infrastructure
implements those abstractions) — but without a CQRS/mediator framework layered on top. Plain
application services calling `DbContext` directly are simpler to read and no less testable for a
project this size; a mediator pipeline is a real cost (indirection, magic string-typed request
routing) that only pays off with a much larger number of use cases than this app will ever have.

Module boundaries are enforced by an automated architecture test (`NetArchTest`, see
`tests/ArchitectureTests`), not by convention — verified working during setup by deliberately
adding an illegal cross-module reference and confirming the test failed with the exact offending
type named, then reverting it.

## Consequences
- Adding a new module later means: new `.csproj` + `.Contracts` project, a project reference from
  `Host`, and nothing else — no changes to how other modules work.
- If a module ever needs to become a real separate service, its `Contracts` project is already the
  shape its public API/DTOs would take — the seam exists before the split does.
- The tradeoff we're accepting: this is still one process, one deployment unit, one failure domain.
  That's correct for this project's size and correct for teaching the *step before* microservices,
  not microservices themselves.
