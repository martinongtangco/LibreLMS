# Implementation Plan: Org Chart Layout (spec 059)

**Branch**: `story/059-org-chart-layout` (spike execution on `spike/speckit-verification`) | **Date**: 2026-11-21 | **Spec**: `specs/059-org-chart-layout/spec.md`

**Note**: spike artifact — written by hand in place of the `/speckit.plan` run so the
oracle stage has its contractually allowed input (the public interface below). The
plan deliberately contains **no implementation details, no algorithm steps, and no
internal class names** beyond the public surface: the oracle must compute expected
values from `spec.md` + this section only (Principle XVIII.1).

## Summary

Reposition `TreeLayoutService.ComputeLayout` so every org shape (chains, wide
fan-outs, multi-sibling grids, forests, cycles) lays out collision-free, per the
spec's layout contract L1–L6, with deterministic coordinates and guaranteed
termination on cyclic input.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.103, `global.json`)
**Primary Dependencies**: none new (FsCheck 3.6.0 dev-dependency, `Management.Tests`)
**Storage**: none (pure service — no EF context in the changed code path)
**Testing**: xUnit 2.9.3 + FsCheck 3.6.0 (`tests/Management.Tests`)
**Target Platform**: server (Host), unit-verified

## Constitution Check

- Principle III (module boundaries): change stays inside `Management` — the module
  boundary is untouched (enforced by `tests/ArchitectureTests`).
- Principle XIII (verification before claim): the full gate stack (visible + holdout
  + attack + mutation, XIX.1) is the claim evidence.
- Principle XVII (disposable environment): the changed code path is pure; the
  mutation gate records "disposable DB: not required" for this feature.
- Principle XVIII (oracle independence): tests are written by the oracle from
  `spec.md` + this plan's Public Interface only.
- Principle XIX (quality gates): mutation gate on the diff; survivors closed by
  tests or spec decision only.

## Public Interface *(oracle's only allowed code-side input — signatures and type
shapes only; implementation bodies are off-limits to the oracle)*

```csharp
// src/Modules/Management/Application — the unit under test (public surface only)
public class TreeLayoutService
{
    // The ONLY public member. Pre-fix and post-fix signature is identical.
    public IList<(Organization Org, int X, int Y, int Depth)> ComputeLayout(IList<Organization> orgs);
}

// src/Modules/Management/Domain — input shape (read-only for the oracle)
public class Organization : Entity<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public ICollection<Organization> Children { get; set; }   // authoritative tree (spec L4)
    public bool IsDeleted { get; set; }
    // (Entity<Guid> provides: Guid Id, DateTimeOffset CreatedAt — see domain)
}
```

No other public surface of the feature is relevant to the spec's contract.

## Project Structure (this feature)

- `specs/059-org-chart-layout/` — spec (done), plan (this file), oracle-report,
  attack-report, mutation-report (to be produced by the gate stages).
- `src/Modules/Management/Application/TreeLayoutService.cs` — the implementation
  change (implementer's lane only).
- `tests/Management.Tests/OrgChartLayoutTests.cs` — visible suite (oracle-owned).
- `tests/Management.Tests/Holdout/OrgChartLayoutHoldoutTests.cs` — holdout (oracle-owned).
- `tests/Management.Tests/Attack/` — red-team regression tests (red-team lane).

## Complexity Tracking

| Complexity | Justification |
|------------|---------------|
| none | single pure service, no new abstractions (Principle II) |
