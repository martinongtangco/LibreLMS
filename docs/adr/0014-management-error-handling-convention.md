# ADR-0014: Management Application error handling — typed exceptions, one endpoint translation point

**Status**: Accepted
**Date**: 2026-10-05
**Supersedes**: none
**Spec**: 058 (code review remediation, US3/FR-003)

## Context

Two error-reporting conventions coexist in the codebase:

1. **Typed exceptions** for expected business failures, thrown by Management
   Application services (`ForbiddenAccessException` for out-of-scope access, BCL
   `KeyNotFoundException` for missing entities, `InvalidOperationException` /
   `ArgumentException` for invalid operations) and translated to HTTP
   responses per-handler in `Program.cs`.
2. **`SharedKernel.Result` / `Result<T>` and per-operation result records** on
   the Scorm session/registration and package surfaces, where the endpoint
   genuinely needs more than a status string (`sessionId`, `committedAt`,
   `status`, `score`, multi-field launch outcomes).

The code review behind this spec cited the repeated catch blocks in
`Program.cs` (the `users` handlers, lines 462-543, as the exemplar): the same
three exception types mapped to the same three status/body shapes were
re-copied across ~10 handlers in the `users`, `orgs`, and `adminCourses`
groups. The question was which convention to standardize on, and whether to
refactor the duplication.

Constraints from the spec: the bar is "a documented decision + at least one
applied refactor with zero observable HTTP change" — explicitly **not** a
wholesale rewrite. `SharedKernel.Result<T>` carries only an `Error` string;
the four cited blocks map three exception types to **three different
status/body shapes** (403 with JSON body, 404 **without** body, 400 with JSON
body), which a `Result<T>` cannot carry losslessly — an endpoint handed a
`Result<T>` would have to match message strings to recover the distinction
(the Scorm launch endpoint already does exactly this:
`result.Error == "Student is not enrolled in this course."`).

## Decision

**Typed exceptions remain the convention for Management Application
services' expected business failures. Endpoints translate them at a single
shared translation point: `ManagementErrors.Translate(Exception)` in the Host
(`src/Host/ManagementErrors.cs`).**

The mapping (pinned by `tests/Host.Tests/ManagementErrorsTests.cs`):

| Exception                          | Response                                    |
|------------------------------------|---------------------------------------------|
| `ForbiddenAccessException`         | 403, JSON body `{ error: ex.Message }`      |
| BCL `KeyNotFoundException`         | 404, **no body**                            |
| `InvalidOperationException`        | 400, JSON body `{ error: ex.Message }`      |
| `ArgumentException`                | 400, JSON body `{ error: ex.Message }`      |
| anything else                      | rethrow (unchanged 500 path)                |

Each refactored handler catches **the identical exception-type set it catches
today**, with each catch body reduced to a one-line delegation to the mapper
(C# has no `or`-pattern catch clause — verified against net10.0 — so the type
set stays one catch block per type, but the mapping itself lives in exactly
one place):

```csharp
catch (ForbiddenAccessException ex) { return ManagementErrors.Translate(ex); }
catch (KeyNotFoundException ex) { return ManagementErrors.Translate(ex); }
catch (InvalidOperationException ex) { return ManagementErrors.Translate(ex); }
```

Because the per-handler type sets are unchanged, no exception that previously
escaped to the 500 path is now translated, and no exception that was
translated previously escapes. The 403 stays a real JSON response (never
`Results.Forbid()` — cookie authentication would 302 it).

`Result`/`Result<T>` and the per-operation result records stay exactly where
they are today — the Scorm session/registration and package surfaces — and
are not extended to the Management layer.

## Why not the alternatives

- **Convert Management services to `Result<T>`**: the status/body distinction
  (403-vs-404-vs-400, body-vs-no-body) would move into message strings,
  trading ~10 duplicated catch blocks for fragile string matching — a worse
  trade. The typed exceptions already carry the distinction losslessly; the
  duplication was in the *translation*, not the signaling.
- **Config-driven exception→status mapper / per-service result-record family**:
  the "clever generalization" Principle II warns against. The convention is
  one sentence — "Management's expected business exceptions are translated
  once, in the Host, into their HTTP responses" — and a four-row `switch`
  expresses it.

## Application scope (this spec)

- `users` group — the four cited handlers (GET `/{id}`, POST `/`, PUT `/{id}`,
  DELETE `/{id}`) — mandatory per FR-003.
- `orgs` group (5 handlers with catches) and `adminCourses` group (3
  handlers) — same uniform shape, collapsed in the same commit set, each
  verified per-handler against its current type set.
- `adminEnrollments` group — **kept as-is with explicit catches**: its failure
  shapes diverge from the mapper (404 **with** body on POST, 409 Conflict on
  duplicate enrollment, `KeyNotFoundException` carrying a message). A comment
  in `Program.cs` points here. Groups whose failure shape diverges keep
  explicit catches by rule.

## Consequences

- Positive: the mapping lives in one test-pinned place; adding a new
  Management handler is a copy of the try/`catch (…) => Translate(ex)` shape
  with no per-exception boilerplate; the four-row contract is regression
  tested independently of any endpoint.
- Neutral: the two conventions still coexist — each in its documented lane
  (typed exceptions = Management business failures; result records = Scorm
  multi-field operational outcomes). New code must pick the lane by the
  endpoint's needs, not by convenience.
- Follow-up candidate (out of scope for spec 058 — "not a wholesale
  rewrite"): converting the remaining throwing Management services
  (`OrganizationService` paths already throw through these endpoints;
  `CourseVisibilityService` likewise) — they already ride this convention;
  the candidate is instead *extending the mapper* if a fourth/fifth expected
  exception type emerges, and converting `adminEnrollments` if its 404/409
  shapes are ever normalized.
- Zero-observable-change proof: per-handler catch type sets identical to
  before (verified against source per handler); the existing Playwright/auth
  suite (including `08-rbac.spec.ts`) is the behavioral gate.
