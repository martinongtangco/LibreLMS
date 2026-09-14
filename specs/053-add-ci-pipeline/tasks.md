# Tasks: Continuous Integration

**Branch**: `story/053-add-ci-pipeline` | **Plan**: [plan.md](plan.md) | **ADR**: [0011](../../../docs/adr/0011-ci-single-job-services-nu1903-error.md)

## US1: The build fails on a known-vulnerable package (NU1903)

- [X] T001 [US1] Baseline (red) evidence: `dotnet build LibreLms.slnx`
      emits NU1903 (record count + advisory URLs); confirm no `.github/`
      directory exists — this is the "before" state for this item
- [X] T002 [P] [US1] `Directory.Build.props` at repo root:
      `Nullable=enable`, `ImplicitUsings=enable` (already universal
      per-project), `<WarningsAsErrors>NU1903</WarningsAsErrors>`,
      `<PackageReference Include="System.Security.Cryptography.Xml"
      Version="9.0.20" />`
- [X] T003 [US1] Verify: `dotnet build LibreLms.slnx` → 0 errors and **0
      NU1903 warnings** (warning-set diff vs baseline = exactly the NU1903
      lines); no other build outcome changes

## US2: The gate stack runs mechanically on push/PR

- [X] T004 [US2] `.github/workflows/ci.yml` per ADR 0011: triggers
      `push` + `pull_request`; single job `build-and-test` on
      `ubuntu-latest`; job `services:` — `mssql`
      (`mcr.microsoft.com/mssql/server:2022-latest`, ACCEPT_EULA,
      throwaway fixture SA password, sqlcmd health check, 1433:1433) and
      `valkey` (`valkey/valkey:8`, `valkey-cli ping` health check,
      6379:6379); job env `ConnectionStrings__Sql` (localhost,1433;
      LearningLms; sa; fixture password; TrustServerCertificate=True),
      `ConnectionStrings__Valkey=localhost:6379`,
      `ASPNETCORE_ENVIRONMENT=Development`
- [X] T005 [US2] Workflow steps, in order: checkout@v4 → setup-dotnet@v4
      (10.0.x) + setup-node@v4 (22) → `dotnet restore LibreLms.slnx` →
      `dotnet build LibreLms.slnx --no-restore` →
      `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
      --no-build` → Host start step (background `dotnet run --project
      src/Host --urls http://localhost:5000` to a log file, bounded poll
      for HTTP 302, log tail + nonzero exit on timeout) → filler-clean
      step (sqlcmd, the same cleanup SQL as the local workflow) → one
      `dotnet test <project> --no-build` each for Catalog, Enrollment,
      Host, Management, Scorm → `npx playwright install chromium
      --with-deps` + `CI=1 npx playwright test` in
      `tests/Playwright.Tests`
- [X] T006 [US2] Validate: YAML parses (parser, not eyeballs); every
      `run` command exists in the local toolchain and matches
      research.md §4 step-for-step

## US3: Gate 2 = the workflow's commands pass locally, in order

- [X] T007 [US3] Local command sequence, in workflow order, with env
      sourced in-shell (`source .../run-env.sh && set +H`): restore →
      build (--no-restore) → ArchTests → **Host start via the
      in-container restart equivalent** (chown obj/bin, detached root
      restart, Now listening + in-container 302 probe) → the five unit
      projects (`--no-build` is the host-built equivalent; run
      `dotnet test <proj>` with env) → filler-clean (11,668-style filler
      rows → 10 seeded courses) → **Playwright via the in-container
      equivalent** (expect 177 passed + 1 documented skip). Record the
      evidence in the run log.
- [X] T008 [US3] Independent verification (Constitution XVI): fresh
      no-context subagent, clean detached worktree on this branch —
      build (0 errors / 0 NU1903), the unit command sequence, Host
      readiness, filler-clean, Playwright. Verdict must be GREEN.
- [X] T009 [US3] Gate 3 (post-merge, on master): rebuild, restart,
      re-run the unit sequence + Playwright; mark all tasks `[X]`, set
      spec Status to Complete, commit F — DONE on master @2843f32: build
      0 errors / 0 NU1903; in-container restart Now listening + 302 probe;
      units 170/170 (Management 55, Host 9, Arch 14, Catalog 32, Enrollment
      42, Scorm 18 — no flake); filler-clean 11,668 → 10 courses;
      Playwright 177 passed + 1 documented skip, 0 failed.
