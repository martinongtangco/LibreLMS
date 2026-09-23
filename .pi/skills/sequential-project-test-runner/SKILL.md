---
name: sequential-project-test-runner
description: Runs LibreLMS test projects one at a time against the shared LearningLms MSSQL database instead of `dotnet test LibreLms.slnx`, which fails non-deterministically under concurrent DB writes. Use for any test gate, baseline check, or CI-equivalent verification step.
disable-model-invocation: true
metadata:
  origin: memory/shared-test-database-no-isolation
---

# Sequential Project Test Runner

## Why

Every test project in this repo connects to the same physical MSSQL database
(`LearningLms`) with no isolation: no xunit `CollectionDefinition`, no
`DisableTestParallelization`, no per-project database, no `.runsettings`. `dotnet test
LibreLms.slnx` runs test projects concurrently and xunit parallelizes classes within each
project, so `Catalog.Tests`, `Enrollment.Tests`, and `Scorm.Tests` collide on shared
tables. The same commit can pass once and fail the next run (observed: `Catalog.Tests`
took 9s standalone, 10m19s under contention). `-m:1` does **not** fix this — it throttles
MSBuild build parallelism, not test scheduling.

## Usage

```bash
./scripts/run.sh
```

Restores the solution once, then runs every test project under `tests/` sequentially with
`dotnet test <project>.csproj --no-restore`, printing per-project passed/failed/skipped
counts and a grand total. Exits non-zero if any project failed.

Treat this script's output — not a solution-wide run — as the trustworthy gate result.

## When NOT to trust a different result

If `dotnet test LibreLms.slnx` (solution-wide) disagrees with this script's output, the
solution-wide run is the environment artifact, not this one. Do not spend retry budget
re-triaging a solution-wide failure this script doesn't reproduce.
