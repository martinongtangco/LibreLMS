# Bug Fix Specification: Configuration Is Not Reproducible; a Secret Is Committed

**Feature Branch**: `bug/051-fix-config-reproducibility`

**Created**: 2026-08-31

**Status**: Draft

**Input**: Hardening-loop handoff, item 3 (all four defects verified against the
code on 2026-08-31):

Four independent configuration defects mean a fresh clone cannot start the app,
and a live database password sits in git history.

## The Four Defects (verified)

1. **Connection-string key mismatch** (fatal for a fresh clone):
   `src/Host/Program.cs` reads `GetConnectionString("Sql")` (4 call sites,
   lines ~35/36/60/63). No file defines `ConnectionStrings:Sql`.
   `appsettings.Development.json` defines `DefaultConnection`, and
   `docker-compose.yml` (devcontainer service) sets
   `ConnectionStrings__DefaultConnection`. `GetConnectionString("Sql")`
   returns `null` → `UseSqlServer(null)` throws at startup. The app only runs
   today because an undocumented `ConnectionStrings__Sql` shell variable
   happens to be exported by the local dev scripts. (Historical note: spec 012
   changed the code side to read `"Sql"`; the config side was never
   finished.)
2. **Committed secret**: `src/Host/appsettings.Development.json` is tracked in
   git and contains the literal MSSQL SA password — the exact leak the
   gitignored `.env` + `.env.example` pattern was introduced to prevent.
3. **Broken devcontainer postCreate**: `.devcontainer/devcontainer.json` runs
   `dotnet restore LearningLms.slnx`; the solution file is `LibreLms.slnx`,
   so container creation always fails that step.
4. **Name/port inconsistencies**:
   - Database name: compose creates `LearningLms`; the committed appsettings
     string says `LibreLms`.
   - Valkey host fallback: `Program.cs` falls back to `localhost:6379`, but
     compose deliberately publishes Valkey on host port **6380** (6379 belongs
     to an unrelated project on this machine — see the compose comment). A
     host-run app with the fallback silently talks to another project's
     Redis instead of failing.

## Fix

1. **One key, `Sql`, everywhere** (the code and every test project already use
   `Sql`/`ConnectionStrings__Sql` — settling on it is the minimal-change
   option):
   - `docker-compose.yml`: `ConnectionStrings__DefaultConnection` →
     `ConnectionStrings__Sql` (same value: `Server=mssql,1433;Database=LearningLms;...`).
   - `appsettings.Development.json`: no `DefaultConnection` entry at all (see
     2); the `Sql` value comes from the environment in both run modes.
2. **Remove the committed secret**: delete the `ConnectionStrings`
   `DefaultConnection` entry from the tracked `appsettings.Development.json`
   (keep the non-secret `Valkey: "valkey:6379"` entry for in-container runs).
   The `Sql` string is sourced from the environment:
   - in-container (devcontainer/compose): the compose env var (defect 1).
   - host runs: documented in the README — export
     `ConnectionStrings__Sql="Server=localhost,1433;Database=LearningLms;User Id=sa;Password=<from .env>;TrustServerCertificate=True"`.
   **Sequencing (handoff hazard)**: the replacement source (compose env var +
   README steps) is verified working *first*; the committed value is deleted
   only after the app demonstrably starts without it. Gate 1 after every one
   of the four changes, not just at the end.
   **The secret remains in git history** — this spec does not rewrite history
   (out of scope, high risk); it recommends **rotating the SA password** and
   records the exposure.
3. **Devcontainer**: `postCreateCommand` → `dotnet restore LibreLms.slnx`.
4. **Consistency**: database name settled on `LearningLms` (the DB compose
   creates and the one the tests use) — now appearing only in compose +
   README; Valkey fallback in `Program.cs` → `localhost:6380` (matches the
   compose host publish and the test-suite fallback).
5. **README**: the "Run the host" section documents the actual working steps
   (env var export from `.env`, in-container alternative).

## Regression guard

A new Host.Tests test asserts that no tracked `appsettings*.json` in the Host
project contains a `Password=` segment in any connection string (secret-scan
regression guard; red-verified against the current committed value).

## Done When

- A clean environment following only the README starts the app (devcontainer
  path: compose env var; host path: documented export).
- The committed `appsettings*.json` contains no secret (test-enforced).
- Gates 1–3 green; the secret's continued presence in history is documented
  with a rotation recommendation.

## Out of Scope

- Git history rewrite / BFG — explicitly not attempted; rotation recommended
  instead.
- user-secrets migration (environment variables are the established source
  here: compose, tests, and dev scripts all use env).
