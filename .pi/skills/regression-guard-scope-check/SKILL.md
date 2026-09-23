---
name: regression-guard-scope-check
description: Before closing a spec whose Done-when includes a regression guard or test, greps the whole tracked tree for other instances of the same defect pattern so the guard covers the class of defect, not just the reported instance. Use after implementing any bug-fix spec, before marking it complete.
disable-model-invocation: true
metadata:
  origin: memory/regression-guards-cover-the-defect-class
---

# Regression Guard Scope Check

## Why

A spec that says "add a regression test" tends to produce a test pinned to the exact
instance named in the spec text, while the same defect survives everywhere else.
Concrete case (spec 051): a committed MSSQL SA password in
`src/Host/appsettings.Development.json` was removed and guarded by
`AppSettingsSecretScanTests.cs`, which scans a hardcoded array of exactly two paths. The
identical live password remained in four other **tracked** files —
`tests/Catalog.Tests/BrowseCoursesSortTests.cs`,
`tests/Enrollment.Tests/AdminListEnrollmentsTests.cs`,
`tests/Enrollment.Tests/AdminListLearnersTests.cs`, and
`specs/015-responsive-mobile-ui/quickstart.md` — as a hardcoded connection-string
fallback. The guard passed; the spec was marked COMPLETED with the secret still in the
repo.

## Steps

1. Identify the concrete pattern the bug represents in a form `git grep` can search for —
   not just the file path named in the spec. For a leaked secret: the secret value or its
   shape (e.g. the password string, or the connection-string fallback pattern). For a
   traversal bug: the vulnerable call pattern, not just the one call site named. For a
   missing validation/null-check bug: the same field or the same unguarded call across
   other handlers.

2. Run `git grep` for that pattern across the **entire tracked tree**, not just `src/` or
   the file(s) the spec mentions:

   ```bash
   git grep -n "<pattern>"
   ```

3. For every additional match outside what the spec's regression test already covers,
   either fix it now if it's in scope for this spec, or explicitly flag it as an unfixed
   instance of the same class in the completion report. Do not let a passing guard imply
   the defect is fully gone when tracked instances remain.

4. If the Done-when check was file-scoped, rewrite it to assert over the *class*: e.g.
   "no tracked file in the repository contains the credential" (verified by `git grep`
   over the tracked tree) rather than "the settings file is clean."

## When to run this

- After implementing any bug-fix spec that adds a regression test or guard, before
  marking the spec's Done-when satisfied.
