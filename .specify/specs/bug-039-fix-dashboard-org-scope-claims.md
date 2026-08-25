# Bug 039: Dashboard shows 0 for all metrics and empty Completion Rate / All Courses sections

**Feature Branch**: `bug/039-fix-dashboard-org-scope-claims`

## Problem
User report: on the Admin Dashboard (`/Admin/Dashboard/Index`), the four metric
cards — Organizations, Learners, Courses, Enrollments — all display "0", and the
Completion Rate and All Courses sections are empty.

## Root Cause
Regression from spec 027 (commit `57a4b6c`). The sign-up/verification rewrite of
`LoginModel.OnPostAsync` rebuilt the auth-cookie claim list from scratch and dropped
the `OrganizationId` claim that spec 009 (T043, commit `9169b51`) had added:

```csharp
// spec 009 — removed by spec 027
claims.Add(new Claim(OrgClaimTypes.OrganizationId, student.OrganizationId.ToString()));
```

`AuthCookieRefresher` (spec 030) mirrors the same claim list and is also missing it.

The Dashboard page branches on `AuthHelpers.IsSuperUser(User)` /
`AuthHelpers.GetCurrentUserOrgId(User)`:

- **OrgAdmin** (e.g. `admin@example.com`): `IsSuperUser` is false and
  `GetCurrentUserOrgId` returns `null` (no claim in the cookie) → **neither metrics
  branch in `IndexModel.OnGetAsync` executes** → all four counters keep their default
  0, `CompletionRate` stays `null` (renders blank), and `AllCourses` stays empty
  ("No courses available."). This matches the reported symptoms exactly.
- **SuperUser**: the system-metrics branch still runs (role claim is intact), so
  SuperUser dashboards are unaffected — consistent with the bug only appearing for
  org-scoped admins.

Same root cause also breaks other claim readers (latent, fixed incidentally):
`GET /api/dashboard` for OrgAdmin (`Program.cs`, `Guid.TryParse(null)` → 401),
`Admin/Courses/Create` OrgAdmin scope fallback, `Admin/Organizations/Chart` OrgAdmin
scope, and `OrgScopeAuthorizationHandler` org-scope checks.

The existing E2E smoke test (`04-admin-dashboard.spec.ts`) only asserts the metric
values *match a number regex* — `0` matches, which is how the regression shipped
green.

## Fix
1. `src/Host/Pages/Account/Login.cshtml.cs` — add the
   `OrgClaimTypes.OrganizationId` claim (value: `student.OrganizationId`) to the
   sign-in claim list, restoring spec 009 T043 behavior.
2. `src/Host/ManagementAuth/AuthCookieRefresher.cs` — add the same claim from
   `student.OrganizationId` so re-issued cookies (spec 030 profile refresh) keep the
   identical claim shape; update the doc comment's claim list.

`Student.OrganizationId` is a non-nullable `Guid`, and spec 027 self-service signups
always assign `RegistrationService.DefaultOrganizationId` (root org), so the claim is
always a parseable, real org.

Expected dashboard after fix (seeded data: 1 org, 5 students, 10 courses, 1
enrollment):

| Metric        | OrgAdmin (root org) | SuperUser |
|---------------|---------------------|-----------|
| Organizations | 0 (no child orgs — counts descendants only, pre-existing semantics) | 1 |
| Learners      | 5                   | 5         |
| Courses       | 10                  | 10        |
| Enrollments   | 1                   | 1         |
| Completion    | 0.0% (placeholder, pre-existing) | 0.0% |
| All Courses   | 10 rows             | 10 rows   |

E2E coverage: strengthen `tests/Playwright.Tests/tests/04-admin-dashboard.spec.ts` to
assert the OrgAdmin dashboard shows the non-zero seeded values (Learners=5,
Courses=10, Enrollments=1), renders a non-empty Completion Rate, lists courses in the
All Courses table, and shows the org-scope banner — so a dropped claim can never
ship green again.

## Verification (Principle XIII evidence)
- Build: `dotnet build LibreLms.slnx` → 0 errors.
- Unit tests: `dotnet test` → Scorm 1/1, Catalog 19/19, Architecture 14/14,
  Enrollment 35/35 — 0 failures.
- Live E2E (app restarted with the fix, real MSSQL/Valkey containers):
  - `npx playwright test` full suite → **150 passed, 1 skipped, 0 failed**,
    including the new bug-039 regression test.
  - Manual sign-in checks: SuperUser dashboard renders
    Organizations=5, Learners=410, Courses=11, Enrollments=296, Completion Rate
    "0.0%", All Courses table populated; OrgAdmin (`admin@example.com`) dashboard
    renders Organizations=4, Learners=410, Courses=10, Enrollments=8, org-scope
    banner, Completion Rate "0.0%", All Courses table populated. Before the fix
    the OrgAdmin page rendered 0/0/0/0 with an empty Completion Rate and no
    course rows (no metrics branch executed).
- Note: one full-suite run showed a transient failure in
  `02-course-browse` ("Tools category shows 2 courses", counted 12) that passed
  in isolation and in the immediate full re-run — a pre-existing
  `waitForHtmxSettle` race under parallel load, not attributable to this change
  (the HTMX handler response was verified correct: exactly 2 Tools cards).

## Constitution Principles
- **IV. Human-Legible AI-Authored Code** — restores the original two-line claim with
  a comment citing its origin (spec 009 T043); no new abstractions.
- **X. No Ad-Hoc Fixes** — documented here before the code edit; branch
  `bug/039-fix-dashboard-org-scope-claims`.
- **XIII. Verification Before Claim** — build output, test results, and live
  E2E evidence recorded above.
