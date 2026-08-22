# Bug Specification: cookies.txt Committed to Public Repository

**Feature Branch**: `bug/033-remove-cookies-file`

**Created**: 2026-08-22

**Status**: In progress

**Input**: User report: "did we just commit the cookies.txt to the remote repo?" — confirmed: the file is in `origin/master` and the repository is public.

## Root Cause

Commit `6212737` (spec 025, 2026-08-12) accidentally committed `cookies.txt` — a libcurl
Netscape cookie jar from local development (containing a `.AspNetCore.Antiforgery` cookie
scoped to `localhost`) — to the repository. `.gitignore` had no entry for cookie files, so
nothing prevented the commit, and it was pushed to the public remote.

## Impact

- The cookie is an **antiforgery token** (not a session/identity credential), scoped to
  `localhost`, and encrypted with the dev app's data-protection keys — practical exploit
  value is negligible.
- It is nonetheless a credential-shaped file in a public repo: a hygiene defect and a
  recurrence risk (any future cookie jar would be commit-able again).

## Proposed Fix (user-approved options A + B)

1. **A — Stop the bleeding**: remove `cookies.txt` from the working tree and index, add it
   to `.gitignore` so it can never be committed again, commit on this branch, merge to
   `master`, push.
2. **B — Full history scrub**: rewrite repository history to remove `cookies.txt` from every
   commit (`git filter-branch` index-filter + reflog expiry + aggressive gc), then
   force-push `master`. Any other remote branch still containing the blob is handled
   (rewritten or deleted if merged) so the file is fully gone from GitHub.

## Verification

- `git log --all --oneline -- cookies.txt` → empty
- `git ls-tree origin/master -- cookies.txt` → empty (post force-push)
- `git cat-file -e <old-blob-sha>` → fails (object purged locally)
- `.gitignore` contains `cookies.txt`; `git status` clean on `master`
- App unaffected (no code change): last Playwright admin suite run remains green
