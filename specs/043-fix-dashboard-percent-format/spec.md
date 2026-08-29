# Bug Fix Specification: Admin Dashboard Completion Rate Format Is Culture-Dependent

**Feature Branch**: `bug/043-fix-dashboard-percent-format`

**Created**: 2026-08-29

**Status**: Draft

**Input**: Discovered while running spec 042's full-suite verification
(pre-merge, T023 prep) on 2026-08-29:
`tests/Playwright.Tests/tests/04-admin-dashboard.spec.ts` —
"OrgAdmin dashboard shows seeded metrics, completion rate, and courses
(bug-039)" fails **deterministically** (isolated run and full runs):

```
Expected pattern: /\d+(\.\d+)?%$/
Received string:  "0.0 %"
```

## Root Cause

Two layers — a latent app defect, exposed by environment drift:

**1. Latent defect (the real bug).** `src/Host/Pages/Admin/Dashboard/Index.cshtml.cs`
formats the completion rate with the .NET **standard** percent format in both role
branches (super-user line ~60, OrgAdmin line ~69):

```csharp
CompletionRate = metrics.AverageCompletionRate.ToString("P1");
```

Standard `P*` formats render through the *ambient* culture's
`PercentPositivePattern` — i.e. the output depends on the server process's
globalization data. The bug-039 regression guard (spec 039/040,
`04-admin-dashboard.spec.ts:101`) pins the **space-free** rendering
(`/\d+(\.\d+)?%$/`). Under the .NET 10 runtime data shipped in the current
devcontainer, **both** the invariant culture and ICU-based en-US render `P1`
with a space before `%` (`"0.0 %"`) — verified empirically in-container
(`dotnet run` probe: invariant `P1` → `[0.0 %]`, en-US `P1` → `[0.0 %]`, on
both the 10.0.0-rc.2 and 10.0.3 runtimes). Consequence: the UI string is
non-deterministic across environments, and the guard can never pass on
.NET 10 as currently formatted.

**2. Trigger (environment drift).** The devcontainer base image is a
**floating tag** (`mcr.microsoft.com/devcontainers/dotnet:1-10.0`; the
Dockerfile itself notes it "bundles whatever SDK build was current when the
image tag was published"). The container was rebuilt when the workspace
crashed on 2026-08-29 (fresh container, up ~47 min at discovery), pulling
newer base-image globalization data. Evidence that this is drift, not a code
regression:

- `git log master..story/042-… -- src/Host/Pages/Admin/Dashboard/
  tests/Playwright.Tests/tests/04-admin-dashboard.spec.ts` → **empty** (spec
  042's branch touches neither file).
- The `P1` format is unchanged since spec 009 (`git log -S 'ToString("P1")'`
  → single commit `9169b51`); the guard regex is unchanged since spec 040.
- Spec 041's completion record: full Playwright suite **151/151 green**
  (commit `6a044b7`) — same test, same format, older container image.

## Fix

Make the rendering **deterministic** by using a **custom** numeric format,
which does not consult culture percent patterns at all:

```csharp
CompletionRate = metrics.AverageCompletionRate.ToString("0.#%");
```

- `"0.#%"` renders `0` → `0%`, `0.3333` → `33.3%`, `1.0` → `100%` —
  space-free in every culture, stable across .NET versions (custom formats
  are pure digit/affix patterns; no `PercentPositivePattern` lookup).
- Matches the existing bug-039 guard contract exactly — **no test change
  needed**.
- `CultureInfo.InvariantCulture` alone is **not sufficient** (verified:
  .NET 10 invariant `P1` still yields `"0.0 %"`) — that is why the fix
  changes the format specifier, not just the culture.
- Cosmetic: zero now renders `0%` instead of `0.0 %`; no layout/CSS depends
  on the old string.

**Out of scope**: a single transient `14-profile-courses` SCORM-launch
failure observed once in the first post-rebuild full-suite run (passed on
isolated re-run and on the second full run). No code defect evidenced; if it
recurs, it gets its own bug spec.

## User Scenarios & Testing

### User Story 1 - Dashboard metrics render identically regardless of server globalization (Priority: P1)

**Acceptance Scenarios**:

1. **Given** the app running in the rebuilt devcontainer (invariant culture
   resolved, `LANG` unset), **When** OrgAdmin opens
   `/Admin/Dashboard/Index`, **Then** the Completion Rate renders
   space-free (e.g. `0%` / `33.3%`) and the bug-039 guard test passes.
2. **Given** the same build, **When** the value is a non-whole percentage,
   **Then** it renders with at most one decimal and no space before `%`.

**Independent Test**: `npx playwright test tests/04-admin-dashboard.spec.ts`
passes (including the bug-039 guard), and the full Playwright suite is green.
