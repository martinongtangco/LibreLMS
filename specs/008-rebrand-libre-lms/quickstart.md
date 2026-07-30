# Quickstart Validation: Rebrand to Libre LMS

**Created**: 2025-07-30
**Feature**: [spec.md](spec.md)

## Prerequisites

- Dev container running (`docker compose up -d`)
- MSSQL and Valkey services available
- Clean build environment

## Validation Steps

### 1. Build the solution

```bash
dotnet restore LibreLms.slnx
dotnet build LibreLms.slnx
```

**Expected**: Clean build with zero errors and zero warnings related to missing namespaces.

### 2. Run architecture tests

```bash
dotnet test tests/ArchitectureTests
```

**Expected**: All module boundary tests pass. No compilation errors from cross-module references.

### 3. Run all tests

```bash
dotnet test LibreLms.slnx
```

**Expected**: All existing tests pass with no failures.

### 4. Verify no "LearningLms" remains in active source

```bash
# Should return zero results (excluding migrations and historical specs)
grep -r "LearningLms" src/ --include="*.cs" --exclude-dir=Migrations
grep -r "LearningLms" tests/ --include="*.cs"
grep -r "Learning LMS" src/Host/Pages/ --include="*.cshtml"
grep -r "Learning LMS" README.md
grep -r "Learning LMS" .specify/memory/constitution.md
```

**Expected**: Zero matches in all four commands above.

### 5. Run the application and check UI

```bash
dotnet run --project src/Host
```

Then open `http://localhost:5000` (or the configured port) and verify:

- Browser tab title shows "Libre LMS"
- Navigation bar displays "Libre LMS" as the brand
- Footer shows "Libre LMS"
- No instance of "Learning LMS" visible on any page

### 6. Verify database connection

After the application starts, check logs for:

- Successful connection to `LibreLms` database
- Database creation (if first run) with seed data
- No errors about missing database or connection failures

## Success Definition

The rebrand is complete when:
1. ✅ Build succeeds with zero errors
2. ✅ All tests pass
3. ✅ No "LearningLms" or "Learning LMS" found in active source or documentation
4. ✅ Application UI consistently shows "Libre LMS"
5. ✅ Application connects to and creates the `LibreLms` database successfully
