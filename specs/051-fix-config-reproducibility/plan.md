# Implementation Plan: Configuration Reproducibility + Committed Secret

**Branch**: `bug/051-fix-config-reproducibility` | **Date**: 2026-08-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/051-fix-config-reproducibility/spec.md`

## Summary

Make a fresh clone start the app using only documented configuration: settle on
one connection-string key (`Sql`, already used by the code and every test
project), remove the committed SA password (sourcing the value from the
environment in both run modes), fix the devcontainer's wrong solution filename,
and make the DB name / Valkey host port consistent. Gate 1 is re-verified after
every change, in the handoff-mandated sequence (new path verified before the
old value is deleted).

## Technical Context

**Language/Version**: C# / .NET 10 (config files, one 1-line code change)
**Primary Dependencies**: none new
**Storage**: unchanged (MSSQL `LearningLms`, Valkey)
**Testing**: existing suites as regression guard + one new secret-scan test (Host.Tests, red-verified)
**Constraints**: the app must keep starting in BOTH run modes — in-container (devcontainer, compose env) and host (shell env var); the secret stays in git history (rotation recommended, no rewrite)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I/II**: config files + one fallback constant + one README section; explainable in one sentence ("the app reads `ConnectionStrings__Sql` from the environment everywhere; no secrets in the repo"). ✅
- **III**: no module boundary touched (Host composition root + repo config). ✅
- **IV**: the key choice is an architectural decision with alternatives — recorded in research.md as a decision (not a full ADR: it's a naming decision, reversible, documented). ✅
- **XIII**: gates with evidence; secret-scan test red-verified; gate 1 after each of the four changes. ✅
- **XIV/XV**: 2-attempt ceiling with triage. ✅

**Post-design re-check**: PASS.

## Project Structure

```text
specs/051-fix-config-reproducibility/
├── plan.md
├── research.md
└── quickstart.md
```

No `data-model.md` (no entities) or `contracts/` (no interfaces).

### Source (files touched)

```text
docker-compose.yml                        # ConnectionStrings__DefaultConnection → __Sql
.devcontainer/devcontainer.json           # postCreateCommand: LearningLms.slnx → LibreLms.slnx
src/Host/Program.cs                       # Valkey fallback localhost:6379 → localhost:6380
src/Host/appsettings.Development.json     # DELETE the DefaultConnection entry (the secret); keep Valkey
README.md                                 # "Run the host" — real working steps
tests/Host.Tests/AppSettingsSecretScanTests.cs  # new: tracked appsettings*.json must not contain Password=
```

## Design — four changes, gate 1 after each (handoff sequence)

### Change 1 — settle the key on `Sql` (compose side) [D1]

`docker-compose.yml` (devcontainer service env):
`ConnectionStrings__DefaultConnection` → `ConnectionStrings__Sql`
(value unchanged: `Server=mssql,1433;Database=LearningLms;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True`).

**Gate 1a**: `docker compose up -d devcontainer` (recreates with the new env);
verify `env` in the container shows `ConnectionStrings__Sql`; start the app
**without** any explicit `ConnectionStrings__Sql` export (bare `dotnet run`
with only `ConnectionStrings__Valkey`) → `Now listening on:` + HTTP probe.
This proves the compose path alone satisfies the code.

### Change 2 — devcontainer solution filename [D1]

`.devcontainer/devcontainer.json`:
`"postCreateCommand": "dotnet restore LibreLms.slnx"`.

**Gate 1b**: run the exact postCreate command in the container
(`dotnet restore LibreLms.slnx` → 0 errors); container recreation optional —
command-level verification is the deterministic part (the image is unchanged).

### Change 3 — Valkey host fallback [D1]

`src/Host/Program.cs` line 74: `?? "localhost:6379"` → `?? "localhost:6380"`.

**Gate 1c**: `dotnet build LibreLms.slnx` 0 errors; app restart in-container
(listening).

→ **Commit D1** (changes 1–3): `fix(051): settle the connection-string key on Sql, fix the devcontainer solution name, and the Valkey host fallback`

### Change 4 — remove the committed secret [D2]

Sequencing per the handoff hazard: Change 1's compose path is verified in
Gate 1a **first**.

1. **Red-verify the guard first**: add
   `tests/Host.Tests/AppSettingsSecretScanTests.cs` — walks up from the test
   base dir to the repo root (the dir containing `.specify`), parses
   `src/Host/appsettings.json` + `appsettings.Development.json`, and asserts no
   `ConnectionStrings` value contains `Password=` (OrdinalIgnoreCase). Run it →
   FAILS (the committed password). Record evidence.
2. `src/Host/appsettings.Development.json`: delete the `DefaultConnection`
   entry (keep `Valkey: "valkey:6379"` — non-secret, in-container convenience).
3. `README.md` "Run the host": document the working steps — in-container path
   (devcontainer: compose supplies the env var, just `dotnet run --project
   src/Host`) and host path (export
   `ConnectionStrings__Sql="Server=localhost,1433;Database=LearningLms;User Id=sa;Password=<MSSQL_SA_PASSWORD from .env>;TrustServerCertificate=True"`
   first). Note the SA-password rotation recommendation (the old value is in
   git history).

**Gate 1d**: in-container app starts with only the compose env var (no
appsettings fallback possible anymore); host-side start with the documented
export (restart-app.sh flow). Secret-scan test now PASSES.

→ **Commit D2**: `fix(051): remove the committed MSSQL SA password from appsettings.Development.json (env-sourced now) + secret-scan regression test + README run steps`

## Testing Strategy

- **New** `AppSettingsSecretScanTests` (red → green, per the house pattern).
- **Regression guard**: full gate 2 — units (Arch 14, Host 9 after the new
  test, Catalog 32, Scorm 18, Enrollment 41 + 1 pre-existing documented
  failure) + full E2E (baseline 172 passed + 1 documented skip; filler-cleaned
  after the last unit run).
- **Clean-environment proof** (the spec's Done-When): Gate 1a's bare-`dotnet
  run` (no explicit Sql export, only compose env) is the in-container proof;
  the README host steps are the host proof (executed in Gate 1d).
