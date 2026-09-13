# Research / Verified Facts — spec 051

All four handoff claims re-verified against the code on 2026-08-31.

- **Key mismatch (fatal)**: `src/Host/Program.cs` lines 35, 36, 60, 63 read
  `GetConnectionString("Sql")`. Nothing defines `ConnectionStrings:Sql`:
  `appsettings.Development.json` defines `DefaultConnection` (with the
  literal SA password and `Database=LibreLms`); `docker-compose.yml` line 23
  (devcontainer service) sets `ConnectionStrings__DefaultConnection`.
  `GetConnectionString("Sql")` → `null` → `UseSqlServer(null)` throws at
  startup. Current runs only work because local scripts export
  `ConnectionStrings__Sql` in the shell.
- **History**: spec 012 ("fix login and seeder") changed the CODE to read
  `"Sql"` (its spec.md documents the inverse mismatch); the config side was
  never updated — this spec finishes that job.
- **Secret**: `appsettings.Development.json` is git-tracked
  (`git ls-files`), password `Lms#vZdV361x...` (matches the `.env`
  `MSSQL_SA_PASSWORD` value — 26 chars). `.gitignore` covers `.env`;
  `.env.example` documents `MSSQL_SA_PASSWORD`. The secret remains in git
  history after this fix — rotation recommended, no history rewrite (scope).
- **Devcontainer**: `postCreateCommand: "dotnet restore LearningLms.slnx"` —
  the solution file is `LibreLms.slnx` (README itself uses the correct name,
  line 45).
- **Inconsistencies**: compose DB `LearningLms` (env + healthcheck target) vs
  appsettings `LibreLms`; Valkey compose host publish `6380:6379` with an
  explicit comment that 6379 belongs to another project, vs the code fallback
  `localhost:6379` (Program.cs line 74). The test suites already use
  `localhost:6380` as their Valkey fallback (house pattern).
- **Who reads what today**:
  - In-container app: the local dev script exports `ConnectionStrings__Sql`
    (Server=mssql,...) + `ConnectionStrings__Valkey=valkey:6379`.
  - Compose (devcontainer env): `__DefaultConnection` (ignored by the code) +
    `__Valkey`.
  - Host runs: only work with a shell-exported `ConnectionStrings__Sql`
    (Server=localhost,1433).
  - Unit tests: `ConnectionStrings__Sql` + `ConnectionStrings__Valkey` env
    vars (every Scorm/Enrollment/Catalog test file).

## Decision: settle the key on `Sql`

- **Decision**: `Sql` is THE key (compose env var, README, code, tests).
- **Rationale**: the code reads `Sql` at 4 sites and every test project reads
  the `ConnectionStrings__Sql` env var (the house pattern, ~8 files);
  renaming to `DefaultConnection` would touch all of them for zero functional
  gain. Spec 012 already moved the code to `Sql`.
- **Alternatives considered**:
  - `DefaultConnection` (the ASP.NET default name): matches framework
    convention but conflicts with the established test-suite env contract;
    larger blast radius.
  - user-secrets (`dotnet user-secrets`): per-machine, not shareable via
    compose/CI, and inconsistent with how this repo already injects the value
    (env everywhere).

## Decision: environment as the secret's source

- **Decision**: `ConnectionStrings__Sql` comes from the environment in both
  run modes (compose for in-container, documented shell export for host);
  the tracked appsettings files carry no connection string with credentials.
- **Rationale**: matches the existing `.env` + `.env.example` pattern the
  repo already adopted for `MSSQL_SA_PASSWORD`; compose already interpolates
  `${MSSQL_SA_PASSWORD}`.
- **Alternatives considered**: user-secrets (see above); a gitignored
  `appsettings.Local.json` (adds an untracked-file dependency per machine —
  env vars are already the convention here).
