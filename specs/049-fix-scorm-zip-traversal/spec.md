# Bug Fix Specification: SCORM Package Upload — Zip Slip Path Traversal + Zip Bomb

**Feature Branch**: `bug/049-fix-scorm-zip-traversal`

**Created**: 2026-08-31

**Status**: Complete (merged 2026-08-31 to master via 975eb00; post-merge gate 3: build 0 errors, E2E 170 passed + 1 documented verify-email skip, unit suites green except 1 pre-existing master failure proven in a clean worktree — see tasks.md Verification Notes)

**Input**: Hardening-loop handoff, item 1 (verified against the code):

SCORM package upload writes archive entries to disk using the attacker-controlled
entry name without validating the resolved path. In
`src/Modules/Scorm/Application/ScormPackageService.cs`, the extraction loop in
`UploadAsync` builds `Path.Combine(contentFullPath, entry.FullName)` at lines 184
(directory entries) and 189 (file entries). A `../` sequence in `entry.FullName`
escapes `wwwroot`; on Windows an absolute entry name discards the base directory
entirely, because `Path.Combine` returns the second argument when it is rooted.
The uploading process can therefore overwrite arbitrary files it has write access
to, including the application's own `appsettings.json` and DLLs. Reachable by any
SuperUser or OrgAdmin via `POST /api/scorm/upload`. There is also no cap on
uncompressed size or entry count, so a zip bomb can fill the disk.

## Root Cause

`UploadAsync` trusts `ZipArchiveEntry.FullName` as a relative path. Two distinct
failures:

1. **Path traversal (zip slip)**: `Path.Combine(contentFullPath, entry.FullName)`
   with `entry.FullName = "../../evil"` or `C:\foo\bar` (Windows) resolves outside
   the intended content directory. Directory entries (names ending in `/`, line
   184) create directories on the same unvalidated path — a traversal *directory*
   entry alone can plant directories outside `wwwroot`.
2. **No decompression bounds**: no cap on total uncompressed bytes and no cap on
   entry count. A high-ratio zip (e.g. 10 GB of zeros from a few KB of archive)
   or a zip with millions of entries fills the disk / exhausts resources.

The endpoint is gated to SuperUser/OrgAdmin, so the attacker class is an
authenticated admin — but the blast radius (arbitrary file overwrite in the app
directory, including `appsettings.json` and DLLs) is a full compromise of the
hosted process, far beyond what the SCORM upload role should allow.

## Fix (as specified by the handoff)

1. Resolve each destination with `Path.GetFullPath` and reject any entry whose
   resolved path does not start with the resolved content directory plus a
   directory separator. Apply the check to **both** the directory-entry branch and
   the file-entry branch (same resolved base).
2. Reject the upload **as a whole** on the first bad entry and clean up the
   partial content directory (no half-extracted package left behind).
3. Add an uncompressed-total-bytes cap and an entry-count cap; both reject the
   upload with a clear error message.

Legitimate SCORM packages (including nested folder structures) must keep working:
the existing packages under `wwwroot/scorm-content/` and `ScormSeeder` content
must still load.

## Acceptance Scenarios

1. **Given** a zip containing an entry whose name traverses out of the content
   directory (`../` prefix or a rooted/absolute name), **When** the package is
   uploaded, **Then** the upload is rejected with a clear error, no file is
   written outside the content directory, and no partial content directory is
   left behind.
2. **Given** a zip whose directory entry (name ending in `/`) traverses out of
   the content directory, **When** the package is uploaded, **Then** the upload
   is rejected and no directory is created outside the content directory.
3. **Given** a zip whose total uncompressed size exceeds the configured cap,
   **When** the package is uploaded, **Then** the upload is rejected with a clear
   error and the partial extraction is cleaned up.
4. **Given** a zip whose entry count exceeds the configured cap, **When** the
   package is uploaded, **Then** the upload is rejected with a clear error.
5. **Given** a well-formed SCORM package with nested folders, **When** it is
   uploaded, **Then** extraction succeeds exactly as before (regression: seeded
   packages under `wwwroot/scorm-content/` still load; SCORM E2E suite green).

## Testing Strategy

- **Unit (tests/Scorm.Tests)**: build an in-memory `ZipArchive` containing a
  traversal entry; assert the upload is rejected and no file is written outside
  the content directory. **Red-verify against the current code first** (the
  established pattern — specs 044, 046, 047 all did it). Second test for the size
  cap (and entry-count cap).
- **E2E guard**: full Playwright suite stays green (170 passed + 1 documented
  verify-email skip baseline) — the legitimate upload/load path is unchanged.

## Out of Scope

- Authz hardening of the upload endpoint itself (it is already role-gated; a
  separate hardening item covers session-level authz).
- Streaming extraction / disk-quota infrastructure — caps are in-process checks.
