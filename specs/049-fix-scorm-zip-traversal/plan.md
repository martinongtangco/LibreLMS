# Implementation Plan: SCORM Package Upload — Zip Slip + Zip Bomb

**Branch**: `bug/049-fix-scorm-zip-traversal` | **Date**: 2026-08-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/049-fix-scorm-zip-traversal/spec.md`

## Summary

Harden `ScormPackageService.UploadAsync` against zip-slip path traversal and
zip bombs: resolve every archive entry's destination with `Path.GetFullPath`,
reject the upload as a whole (and clean up the partial directory) when any
entry resolves outside the content directory, and enforce uncompressed-size and
entry-count caps with clear errors. No behavior change for legitimate packages.

## Technical Context

**Language/Version**: C# / .NET 10 (pinned via global.json)
**Primary Dependencies**: System.IO.Compression (ZipArchive) — no new packages
**Storage**: extracted content under `wwwroot/scorm-content/{packageId}`; `ScormPackages` table unchanged (no migration)
**Testing**: xUnit, real MSSQL via `ConnectionStrings__Sql` (Scorm.Tests house pattern), Playwright as behavior guard
**Project Type**: modular monolith — change lives in the Scorm module's Application layer, called by the Host endpoint
**Constraints**: seeded packages under `wwwroot/scorm-content/` and `ScormSeeder` content must still load; nested-folder SCORM packages must keep extracting

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I (modular monolith)**: change stays inside `Modules/Scorm/Application` + its test project. ✅
- **II (clean architecture, simple)**: fix is explicit control flow in the existing extraction loop; caps are two constructor-bound values, not a new options framework. Explainable in one sentence. ✅
- **III (module boundaries)**: no new cross-module references; `IScormPackageService` surface unchanged (no contract change needed — `UploadAsync` already returns `(package, error)`). ✅
- **IV (legible code + ADR)**: no architectural decision — this is a security fix to an existing method. No ADR required (not a structural choice). ✅
- **V (sandbox)**: all work in-repo; tests use the sibling MSSQL container. ✅
- **VI (polyglot storage)**: no storage change. ✅
- **VII/X (spec-driven, no ad-hoc)**: spec 049 exists, branch `bug/049-...` created at implement time. ✅
- **XIII (verification)**: gate 1 build+restart, gate 2 unit (red-verified) + full suite, gate 3 post-merge. ✅
- **XIV/XV (bounded retry)**: 2-attempt ceiling with triage before retry 2. ✅

**Post-design re-check**: same result — no new dependencies, no boundary changes. PASS.

## Project Structure

### Documentation (this feature)

```text
specs/049-fix-scorm-zip-traversal/
├── plan.md              # this file
├── research.md          # verified facts from code inspection (no open unknowns)
└── quickstart.md        # validation guide
```

No `data-model.md` (no entity/schema change) and no `contracts/` (no interface
change — the upload endpoint's request/response shape is unchanged).

### Source Code (files touched)

```text
src/Modules/Scorm/Application/ScormPackageService.cs   # the fix (UploadAsync loop + caps)
src/Modules/Scorm/Endpoints/ScormModuleExtensions.cs   # cap values through DI (optional params)
tests/Scorm.Tests/ScormUploadTraversalTests.cs         # new: traversal + caps, red-verified
```

**Structure Decision**: single-method hardening in the existing service; no new
types, no new projects.

## Design

### Traversal check (both entry branches)

```text
var contentRoot = Path.GetFullPath(contentFullPath) + Path.DirectorySeparatorChar;
...
var resolved = Path.GetFullPath(Path.Combine(contentFullPath, entry.FullName));
if (!resolved.StartsWith(contentRoot, StringComparison.Ordinal))
    → reject the upload: clean up contentFullPath (Directory.Delete(true)),
      return (null, "SCORM package rejected: entry '<name>' escapes the content directory.")
```

- Applied to **directory entries** (name ends with `/`) and **file entries** alike
  (the directory branch is the hazard called out in the handoff: a traversal
  *directory* entry alone plants directories outside `wwwroot`).
- `Path.GetFullPath` collapses `..`, resolves rooted names on Windows
  (`C:\...` discards the base under `Path.Combine`), and normalizes separators —
  so the `StartsWith(root + separator)` test is sufficient; no string-level `..`
  sniffing.
- Rejection is **all-or-nothing**: on the first bad entry, stop extraction,
  delete the partial `scorm-content/{packageId}` directory (best effort — it
  does not exist before the loop, so deleting it restores the pre-upload state),
  and return the error tuple. The endpoint already maps non-null `error` to
  `Results.BadRequest(new { error })` — no endpoint change.
- Check the path **before** creating directories/writing files for each entry,
  so a bad entry never creates anything.

### Caps

- `maxEntryCount` (default **5,000**): count `archive.Entries` up front
  (before extraction begins); reject with
  "SCORM package rejected: N entries exceeds the maximum of 5,000."
- `maxUncompressedBytes` (default **100 MB**): track the sum of
  `entry.Length` across entries (checked while iterating, so a bomb is rejected
  at the point the running total crosses the cap — no need to read all bytes
  first; `entry.Length` is the declared uncompressed size and the loop already
  copies every byte, so the actual written total is bounded by the cap plus one
  entry). Reject mid-extraction with the same cleanup path.
- Both values flow in through the constructor as optional parameters with the
  defaults as named constants (`DefaultMaxEntryCount`,
  `DefaultMaxUncompressedBytes`); `ConfigureScormModule` passes them through as
  optional parameters so the Host needs no change today but can override later.

### Red-verification (before the fix)

`tests/Scorm.Tests/ScormUploadTraversalTests.cs` (new file, house pattern —
real MSSQL via `ConnectionStrings__Sql`, `MigrateAsync()` in setup, own seed
rows cleaned up):

1. **Traversal file entry**: in-memory `ZipArchive` (temp dir as wwwroot) with
   `imsmanifest.xml` (so manifest parsing passes) + entry
   `../escape.txt` → assert upload returns an error AND no file exists at
   `wwwroot/escape.txt` AND no partial `scorm-content/{id}` dir remains.
   Against current code this test **fails** (file gets written outside) —
   red-verify before implementing.
2. **Traversal directory entry**: entry `../evil-dir/` (and a nested file under
   it) → same assertions on `wwwroot/evil-dir`.
3. **Rooted entry name**: entry `C:\scorm-escape\evil.txt` (Windows) → rejected,
   nothing written at `C:\scorm-escape`.
4. **Size cap**: pass a tiny `maxUncompressedBytes` (e.g. 1,000) via the
   constructor with a zip whose total uncompressed size (a few KB of real
   bytes) exceeds it → rejected with the size message, partial dir cleaned.
   (Bounding on the declared `entry.Length` is what the implementation checks;
   the tiny-cap variant exercises the same code path with real bytes.)
5. **Entry-count cap**: pass a tiny `maxEntryCount` (e.g. 2) via the constructor
   with a 3-entry zip → rejected with the count message.
6. **Legitimate nested package**: normal nested-folder zip → succeeds; files
   land under the content dir; package row created (regression guard).

## Testing Strategy

- Unit: the six tests above (new file, red-verified for 1–3).
- E2E guard: full Playwright suite (170 passed + 1 documented verify-email skip
  baseline) — legitimate SCORM upload/launch paths unchanged (specs 02, 15).
