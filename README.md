# Libre LMS

A small Learning Management System (course catalog, SCORM launch, per-user completion) — built as
a hands-on exercise in two things:

1. **Spec-driven development** with [GitHub's spec-kit](https://github.com/github/spec-kit)
   (`/speckit.specify → /speckit.plan → /speckit.tasks → /speckit.implement`), driven by
   [Pi Agent CLI](https://github.com/badlogic/pi-mono) against a local Qwen model.
2. **Sandboxing an AI coding agent** — the agent never runs directly on the host; it runs inside
   the Dev Container defined below, which can see this repo and nothing else on the machine.

Read `.specify/memory/constitution.md` first — it's the actual source of truth for *why* things
are structured the way they are. The short version, and the reasoning behind each point, is in
`docs/adr/`.

## Architecture at a glance

- **Modular monolith**: one process (`src/Host`), three independent modules
  (`src/Modules/{Catalog,Enrollment,Scorm}`), each with a thin `*.Contracts` project as the only
  thing another module is allowed to reference. See [`docs/adr/0001`](docs/adr/0001-modular-monolith-clean-architecture.md).
- **Clean Architecture inside each module** (Domain → Application → Infrastructure/Endpoints),
  kept deliberately free of CQRS/mediator frameworks — see the same ADR for why.
- **Boundaries are compiled, not conventional**: `tests/ArchitectureTests` fails the build if a
  module ever references another module's internals directly instead of its `.Contracts` project.
- **Storage**: MSSQL is the system of record; Valkey (Redis-protocol) holds only the live SCORM
  session bag for an in-progress attempt. See [`docs/adr/0003`](docs/adr/0003-polyglot-storage-mssql-redis.md).

## Running this locally

### 1. Open in the Dev Container (this is the sandbox — see [`docs/adr/0002`](docs/adr/0002-agent-sandboxing-devcontainer.md))

```bash
cp .env.example .env   # then set a real MSSQL_SA_PASSWORD
```

Then either:
- VS Code: "Dev Containers: Reopen in Container", or
- CLI: `devcontainer up --workspace-folder .` (requires the `@devcontainers/cli` npm package)

This also brings up `mssql` and `valkey` as sibling containers.

### 2. Build and test

```bash
dotnet restore LibreLms.slnx
dotnet build LibreLms.slnx
dotnet test tests/ArchitectureTests   # the module-boundary check
```

### 3. Run the host

The app reads its database connection from the `ConnectionStrings__Sql`
environment variable (spec 051) — there is no connection string committed to
the repo, on purpose (the SA password must not live in git).

**Inside the Dev Container** — compose already exports `ConnectionStrings__Sql`
(points at `mssql:1433` with the password from your `.env`), so just:

```bash
dotnet run --project src/Host --urls http://localhost:5000
```

**On the host machine** — compose publishes MSSQL on `localhost:1433`; export
the variable from your `.env`, then run:

```bash
export ConnectionStrings__Sql="Server=localhost,1433;Database=LearningLms;User Id=sa;Password=$(grep MSSQL_SA_PASSWORD .env | cut -d= -f2-);TrustServerCertificate=True"
dotnet run --project src/Host --urls http://localhost:5000
```

The same variable is what the test suites expect (`ConnectionStrings__Valkey`
defaults to `localhost:6380`, the compose-published Valkey port).

> **Security note (spec 051)**: a previous revision of this repo committed the
> live SA password in `appsettings.Development.json`. It has been removed, but
> the value remains in git history — **rotate the SA password** (see
> `specs/051-fix-config-reproducibility/quickstart.md`) and never commit
> credentials again.

## Development workflow

Every feature goes through spec-kit before any code gets written (constitution Principle VII):

```
/speckit.specify   Slice N: <capability>
/speckit.plan
/speckit.tasks
/speckit.implement
```

Slices so far:
- **Setup** (this commit): repo, sandbox, spec-kit scaffold, module skeleton — no business logic yet.
- **Slice 1** (next): Course Catalog + Enrollment.
- **Slice 2**: SCORM Launch & Completion.

## Repo layout

```
.devcontainer/       the sandbox: Dockerfile + devcontainer.json
.specify/             spec-kit scaffold (constitution, templates, workflow)
.pi/prompts/          spec-kit slash commands for Pi Agent CLI
docs/adr/             why things are built the way they are
docker-compose.yml     devcontainer + mssql + valkey
src/
  Modules/{Catalog,Enrollment,Scorm}/   each: Domain/ Application/ Infrastructure/ Endpoints/
  Modules/{Module}.Contracts/           the only cross-module-visible surface per module
  Host/                                composition root + web portal
  SharedKernel/                        Entity<TId>, Result<T>, IDomainEvent — nothing else
tests/
  {Module}.Tests/        per-module unit tests (empty until their slice lands)
  ArchitectureTests/      the compiled module-boundary check
```
