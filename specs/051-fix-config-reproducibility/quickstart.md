# Quickstart: spec 051 validation

## Prerequisites

- Docker services up (`docker compose up -d`)
- `.env` with a real `MSSQL_SA_PASSWORD` (compose interpolates it)

## Validation (in order — gate 1 after EACH change)

1. **Change 1 (compose key)**: `docker compose up -d devcontainer` (recreate);
   in the container:
   ```
   env | grep ConnectionStrings        # ConnectionStrings__Sql present
   # bare start — NO explicit Sql export:
   ConnectionStrings__Valkey=valkey:6379 dotnet run --project src/Host --urls http://localhost:5000
   ```
   → `Now listening on:` + HTTP probe (302). Proves the compose env var alone
   satisfies `GetConnectionString("Sql")`.
2. **Change 2 (devcontainer)**: in the container, `dotnet restore
   LibreLms.slnx` → 0 errors.
3. **Change 3 (Valkey fallback)**: `dotnet build LibreLms.slnx` → 0 errors;
   app restart in-container → listening.
4. **Change 4 (secret removal)**:
   - Red first: `dotnet test tests/Host.Tests --filter
     "FullyQualifiedName~AppSettingsSecretScanTests"` → FAILS (committed
     password found).
   - Delete the `DefaultConnection` entry; update README.
   - Same test → PASSES.
   - In-container app starts with only the compose env var; host-side start
     works with the documented export:
     ```
     export ConnectionStrings__Sql="Server=localhost,1433;Database=LearningLms;User Id=sa;Password=$(grep MSSQL_SA_PASSWORD .env | cut -d= -f2-);TrustServerCertificate=True"
     dotnet run --project src/Host --urls http://localhost:5000
     ```
5. **Gates 2–3**: standard (units, full E2E with filler-clean AFTER the last
   unit run, independent verification, merge, post-merge re-run).

## Post-merge hygiene (documented, out of scope for the code change)

- Rotate the SA password (the old value is in git history). The MSSQL image
  applies `MSSQL_SA_PASSWORD` only at FIRST init, so with the named volume in
  place, rotate inside the running container, then update `.env`:
  ```
  docker exec sbxtestwspeckit-mssql-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<old>' -C -Q "ALTER LOGIN sa WITH PASSWORD = '<new>'"
  ```
  (then set `MSSQL_SA_PASSWORD=<new>` in `.env` so compose healthchecks and
  the app's env stay in sync).
