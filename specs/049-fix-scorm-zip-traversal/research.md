# Research / Verified Facts — spec 049

No open unknowns: the handoff's analysis was re-verified against the code on
2026-08-31.

- **Extraction loop** (`ScormPackageService.UploadAsync`, current file lines
  ~176–199): directory branch `Path.Combine(contentFullPath, entry.FullName)` →
  `Directory.CreateDirectory`; file branch same combine → `CreateDirectory` on
  parent → `FileStream` write. Both branches unvalidated. Confirmed.
- **Windows rooted-name behavior**: `Path.Combine(base, "C:\x")` returns
  `"C:\x"` (rooted second arg discards the base). Confirmed by .NET docs +
  probe at implement time via the red test.
- **Error surfacing**: endpoint `POST /api/scorm/upload` (Program.cs ~line 307)
  maps non-null `error` from the `(package, error)` tuple to
  `Results.BadRequest(new { error })`. No endpoint change needed — the
  rejection messages just flow through the existing tuple.
- **DI**: `ScormPackageService` is registered in
  `src/Modules/Scorm/Endpoints/ScormModuleExtensions.ConfigureScormModule(services, wwwRootPath)`
  (both concrete type and `IScormPackageService`). Caps are added as optional
  parameters there — no Host change required.
- **Test pattern**: `tests/Scorm.Tests` uses real MSSQL via the
  `ConnectionStrings__Sql` env var with `ctx.Database.MigrateAsync()` in
  `InitializeAsync` (house pattern from specs 046/048); each test cleans up its
  own seed rows. The upload-under-test only needs the DB for the final
  `ScormPackages.Add` — real DB keeps the test honest.
- **Caps are not configurable today** and the spec does not require
  configurability: named constants + optional constructor parameters (one
  sentence: "the upload service takes two optional limits, defaulting to
  5,000 entries and 100 MB uncompressed"). No config plumbing, no options
  class (Constitution II).

## Decision: reject-on-first-bad-entry with directory cleanup

- **Decision**: validate each entry's resolved path before touching the disk;
  on the first violation, `Directory.Delete(partialDir, true)` (best effort)
  and return the error tuple.
- **Rationale**: partial extraction of an attack payload is worse than a clean
  rejection; the content dir is brand-new per upload (`{Guid}`), so deleting it
  exactly restores the pre-upload state.
- **Alternatives considered**:
  - Validate all entries before extracting anything (two passes): strictly
    safer against a "good-then-bad" ordering, but with the per-entry pre-check
    plus cleanup the observable end state is identical (no files outside the
    root, no partial dir) and the single-pass version is simpler.
  - Stream each entry through a path-checking wrapper: same thing, more code.
