# UI Contracts: Organic Design System Redesign

**Feature**: 017-organic-ui-redesign
**Date**: 2026-08-03

This feature adds no new public HTTP API endpoints (no changes to `Program.cs` minimal-API routes
or `*Endpoints.cs` files). Its "interfaces" are Razor Page routes and the Application-service
methods those pages call. Documented here for the same reason an API spec would be: so
`/speckit.tasks` and `/speckit.implement` have a single source of truth for what each page/handler
does.

## Page routes

| Route | Page Model | Handlers | Auth | Change |
|-------|-----------|----------|------|--------|
| `/MyCourses` | `MyCoursesModel` | `OnGetAsync`, `OnGetEnrollmentsAsync` (HTMX partial) | Authenticated | View only — restyle `Index.cshtml`/`_EnrollmentList.cshtml` to Organic cards; page model unchanged. |
| `/Courses` | `Courses/IndexModel` | `OnGetAsync` (search/category query params) | Public/Authenticated | View only — restyle toolbar + card grid. |
| `/Courses/Detail` | `Courses/DetailModel` | `OnGetAsync`, enroll POST handler | Authenticated (enroll action) | View only — restyle hero + CTA. |
| `/Admin/Dashboard` | `Admin.Dashboard.IndexModel` | `OnGetAsync` | `SuperUser`, `OrgAdmin` | View + model — add `AllCourses: List<CourseRow>` (title, category, hours, enrollment count) populated via new `GetEnrollmentCountsByCourseAsync`. |
| `/Account/Profile` *(new)* | `Account.ProfileModel` | `OnGetAsync` | Authenticated | New page — reads name/email/role from the current `ClaimsPrincipal`, no writes. |
| `/Account/Settings` *(new)* | `Account.SettingsModel` | `OnGetAsync`, `OnPostAsync` | Authenticated | New page — `OnGetAsync` calls `GetPreferencesAsync`; `OnPostAsync` calls `UpdatePreferencesAsync` and redisplays the page (PRG pattern, matching existing form-post pages like `Admin/Courses/Create`). The page's "Logout" row posts to the existing `/Account/Logout` handler unchanged. |
| `/Account/Login`, `/Scorm/Launch`, `/Admin/Organizations/*`, `/Admin/Learners/*`, `/Admin/Courses/*` (except Dashboard), `/Admin/Enrollments/*`, `/Admin/Upload` | (unchanged) | (unchanged) | (unchanged) | Out of scope — inherit only the shared `_Layout.cshtml` nav restyle (FR-001/FR-017). |

## Shared layout contract

| Element | Contract |
|---------|----------|
| `_Layout.cshtml` nav | Renders brand, role-gated page links (unchanged link set/roles — see spec Assumptions), a hamburger toggle at ≤760px collapsing the links (unchanged behavior from spec 015/016), and an avatar+name control. |
| Avatar/profile control | Always rendered when `User.Identity.IsAuthenticated`; opens a dropdown with exactly two items: "View Profile" → `/Account/Profile`, "Settings" → `/Account/Settings`. Renders nothing (falls back to the existing "Login" link) when unauthenticated. |
| Avatar initials | First letter of the first and last whitespace-separated tokens in `ClaimTypes.Name` (e.g. "Alice Johnson" → "AJ"); single-token names use its first letter only; falls back to "?" if the name claim is empty. |

## Application-service contract additions

| Module | Method | Signature | Called by |
|--------|--------|-----------|-----------|
| Enrollment | `GetPreferencesAsync` | `Task<(bool EmailNotificationsEnabled, string ThemePreference)> GetPreferencesAsync(Guid studentId)` | `Account.SettingsModel.OnGetAsync` |
| Enrollment | `UpdatePreferencesAsync` | `Task UpdatePreferencesAsync(Guid studentId, bool emailNotificationsEnabled, string themePreference)` | `Account.SettingsModel.OnPostAsync` |
| Enrollment | `GetEnrollmentCountsByCourseAsync` | `Task<IReadOnlyDictionary<Guid,int>> GetEnrollmentCountsByCourseAsync(IEnumerable<Guid> courseIds)` | `Admin.Dashboard.IndexModel.OnGetAsync` |

No changes to `Enrollment.Contracts`, `Catalog.Contracts`, `Scorm.Contracts`, or `Management.Contracts` — all three additions are called directly by `Host` page models against the owning module's `Application` service, matching the existing `MyCoursesModel` precedent (see research.md §5).
