# Research: Organic Design System Redesign

**Feature**: 017-organic-ui-redesign
**Date**: 2026-08-03

## 1. Design token mechanism

**Decision**: Extend the existing `:root` CSS custom-property block in `src/Host/wwwroot/css/site.css` with the Organic system's values, rather than introducing a separate stylesheet, CSS framework, or component library.

**Rationale**: `site.css` already implements a token-driven design (`--color-*`, `--font-*`, `--spacing-*`, `--border-radius`, `--shadow-card`) — the exact mechanism the design handoff's `--color-bg`/`--color-accent`/`--font-heading`/`--radius-lg` tokens assume. Retargeting the existing tokens (and adding the handful the Organic system introduces that don't exist yet: `--color-accent-2`, `--font-heading`, `--radius-lg`/pill) is a same-file value swap, not a rewrite. This matches Constitution Principle II (don't add a layer/abstraction the codebase doesn't already need).

**Alternatives considered**:
- Ship the handoff's standalone `_ds/organic-.../styles.css` design-system bundle as-is: rejected — it's a prototype tool's bundled CSS (`_ds_bundle.js`) built for the `Libre LMS.dc.html` mock, not meant to be vendored into a real ASP.NET Core `wwwroot`; it would duplicate the token system already in place.
- Introduce a CSS framework (Tailwind, Bootstrap theme): rejected — no existing dependency, adds a build step the constitution's "fewest moving parts" guidance argues against for a slice this size.

## 2. Web fonts (Caprasimo, Figtree)

**Decision**: Self-host both fonts as static files under `wwwroot/fonts/` with `@font-face` declarations in `site.css`, falling back to the existing system font stack if a font file fails to load.

**Rationale**: The app currently ships zero external network calls for assets (SCORM content is local, HTMX is the one CDN `<script>` tag already in `_Layout.cshtml`). Self-hosting keeps the same offline/sandbox-friendly posture as the rest of the app instead of adding a Google Fonts network dependency for every page load.

**Alternatives considered**: Google Fonts `<link>` — rejected, adds an external runtime dependency and a render-blocking third-party request the project doesn't currently have.

## 3. Deriving course status/progress for My Courses

**Decision**: Reuse the existing `MyCoursesModel` join (`EnrollmentService.GetMyEnrollmentsAsync` + `ScormAttemptService.GetMyAttemptsAsync`, latest attempt per course) unchanged. Map the resulting `LatestStatus`/`LatestScore` to the card's tag and progress bar as:
- No attempt → tag "Not Started", progress 0%.
- `LatestStatus` via `ScormHelpers.GetDisplayLabel` → "In Progress"/"Completed"/"Passed"/"Failed"/"Abandoned"/"Browsed"; the design only defines two tag colors (neutral / sage), so "Not Started" (and no attempt) renders `tag-neutral`, every other status renders `tag-accent-2` (sage).
- Progress percentage = `LatestScore` (existing `CourseAttempt.ScoreRaw`, 0–100) when present, else 0 for "Not Started", else 100 for a "Completed"/"Passed" status with no recorded score.

**Rationale**: `ScormHelpers` already has `GetDisplayLabel`/`GetDisplayPercentage`/`GetStatusBadgeColors` mapping SCORM status/score to display strings — this is exactly the derivation FR-004 needs, just re-themed for the Organic tag classes instead of the current inline hex-color badges. No new domain concept (no fabricated "progress model") is introduced.

**Alternatives considered**: Add a first-class `ProgressPercent` field computed and stored on `CourseAttempt`: rejected — `ScoreRaw` already is that number where SCORM content reports it; adding a parallel stored field would duplicate data with no new information.

## 4. Settings persistence scope

**Decision**: Add two simple columns directly to the existing `Student` entity (`Enrollment.Domain.Student`) — `EmailNotificationsEnabled` (`bool`, default `true`) and `ThemePreference` (`string`, default `"System"`) — rather than a new `UserPreference` entity/table.

**Rationale**: Two scalar fields on the entity that already represents "the user" is simpler than a new one-to-one child table, per Constitution Principle II ("don't wrap in another layer unless a specific, current problem requires it"). The spec's Key Entities section calls this "User Preference" conceptually; at the implementation level it's fields on `Student`, not a new table.

**Alternatives considered**: New `UserPreferences` table with a FK to `Student`: rejected as unwarranted normalization for two fields with no independent lifecycle.

**Theme functionality note**: Only the single Organic light theme has design tokens today. `ThemePreference` is stored and displayed (so the Settings UI is real, not decorative) but selecting a non-default value does not yet change rendered appearance — implementing an actual dark palette is out of scope per the spec's Assumptions.

## 5. Admin Dashboard course table (enrollment counts)

**Decision**: Add `EnrollmentService.GetEnrollmentCountsByCourseAsync(IEnumerable<Guid> courseIds)` (single grouped `COUNT(*) GROUP BY CourseId` query) to the `Enrollment` module. `Admin/Dashboard/IndexModel` calls this alongside the existing `CourseVisibilityService.GetAllCoursesAsync()` (Management) and zips the results by `CourseId`, the same way `MyCoursesModel` already zips `EnrollmentService` + `ScormAttemptService` results.

**Rationale**: Keeps the enrollment count computation inside the module that owns `Enrollment` rows, avoids a new cross-module `*.Contracts` interface (Management doesn't need to see individual enrollments, only a count keyed by course id it already has from its own course list), and avoids N+1 queries (one grouped query for all visible courses, not one query per row).

**Alternatives considered**: Extend `IEnrollmentLookup` (Enrollment.Contracts) with a count method so `Management`'s own service could call it directly: rejected for this slice — `Host` combining two module services in the page model is the pattern already established for `MyCoursesModel`, and introducing a new contract method for a single UI table is more machinery than the read requires right now. Revisit if a second consumer outside `Host` needs the same aggregate.

## 6. Nav profile control on mobile

**Decision**: The avatar/profile dropdown stays in its own top-right control at all breakpoints (unchanged position on mobile); only the page links + collapse behind the hamburger, matching the existing `_Layout.cshtml` mobile pattern (hamburger toggles `#nav-links`) and the design handoff's mobile spec (hamburger menu holds page links, profile avatar/name stays visible).

**Rationale**: Matches both the current implementation's mobile nav structure and the design handoff's explicit mobile description — no redesign of the mobile interaction model itself, only its visual skin (Constitution Principle VII: this is a styling slice, not a re-architecture of navigation).

**Alternatives considered**: Fold the profile menu into the hamburger menu too: rejected — contradicts the design handoff, which keeps the avatar visible outside the hamburger on mobile.

## Outstanding NEEDS CLARIFICATION

None. All Technical Context unknowns above were resolved with a documented decision and rationale.
