# ADR-0013: Explicit challenge guards for handler-level page authz; no silent identity fallbacks

**Status**: Accepted
**Date**: 2026-09-20
**Supersedes**: none
**Spec**: 055 (fix enrollment while logged out)

## Context

A signed-out visitor could enroll in courses, and the enrollment was silently
attributed to the seeded demo learner (`550e8400-…-0001`, alice@example.com).
Root-cause analysis (spec 055, research.md R1/R2) found two independent design
defects, both verified against the running app (runtime `Microsoft.AspNetCore.App
10.0.3`) and a minimal framework repro:

1. **Handler-level `[Authorize]` is inert in .NET 10.** The enroll action
   (`CourseDetailModel.OnPostEnrollAsync`) carried `[Authorize]`, yet an anonymous
   POST executed the handler (200, enrollment created). Control group in the same
   app: class-level `[Authorize]` (e.g. `/Account/Profile`) challenges correctly
   (302 → login), and minimal-API `[Authorize]` (`POST /api/enrollments`) as well
   (401 + Location). A minimal Razor Pages app (fresh project, same runtime)
   reproduces it: `[Authorize]` on `OnPost` does not challenge a guest POST, while
   the class-level attribute does. Framework source
   (`AuthorizationApplicationModelProvider`): with endpoint routing enabled (the
   default), attribute→filter conversion is skipped — authorization is decided from
   endpoint metadata, and Razor Pages does not copy per-handler `IAuthorizeData`
   attributes into per-handler selector metadata. Net effect: **a page handler with
   `[Authorize]` is anonymous unless the class/page is authorized too** — and the
   failure mode is silent (the handler just runs).

2. **Silent identity fallback.** `ScormHelpers.GetStudentId(HttpContext)` (and a
   private duplicate in `Account/Settings.cshtml.cs`) returned a hardcoded demo
   GUID when no identity claim parsed. Any code path that skipped authentication
   then acted as the demo learner: anonymous enrollments were created for
   alice@example.com, and guest read paths (catalog badges, course page,
   `/MyCourses`, which had no `[Authorize]` at all) displayed that learner's data.

## Decision

1. **Explicit challenge guards for handler-level authorization.** Where a page
   handler (not the whole page) requires a signed-in learner, the handler's first
   statement is an explicit guard:

   ```csharp
   if (User.Identity?.IsAuthenticated != true)
       return new ChallengeResult("Cookie");
   ```

   The inert `[Authorize]` attribute on such handlers is removed — keeping it
   implies a guarantee the platform does not honor. Class-level `[Authorize]` (which
   works) remains the default for fully-protected pages (e.g. `/MyCourses`).

2. **Identity resolution never substitutes an identity.** `GetStudentId` returns
   `Guid.Empty` (the codebase's existing "no learner" sentinel) when no parseable
   claim exists; callers decide what "no learner" means (enroll → challenge; read
   paths → not-enrolled state). The demo learner stays a normal seeded account for
   tests/demos — it simply can no longer be impersonated by an anonymous visitor.

## Consequences

- **Positive**: the authz decision is visible in the code (a reviewer sees the
  guard), a forgotten guard is caught by the E2E contract
  (`21-logged-out-enroll.spec.ts`), and no code path can silently act as another
  identity. Guest read paths degrade to the public state with zero query waste.
- **Negative / accepted**: each handler-level guard is one explicit statement
  instead of one attribute — more visible, slightly more verbose. If a future
  .NET release starts honoring handler-level attributes, the guards remain correct
  (redundant, not wrong) — they are the house pattern either way.
- **Follow-up (noted, not in 055)**: if handler-level authz is needed on more
  pages, consider a tiny `[RequireLearner]`-style custom attribute that performs
  the same explicit challenge — but only when the repetition justifies it
  (Principle II).
