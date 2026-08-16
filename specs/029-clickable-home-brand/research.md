# Research: Clickable Brand Link to Home

**Feature**: 029-clickable-home-brand
**Date**: 2026-08-16

## 1. Brand Link Target: Site Root vs. Direct Page

**Decision**: Link the brand to the site root (`href="/"`). The root already
redirects to Browse Courses (`app.MapGet("/", () => Results.Redirect("/Courses"))`
in `src/Host/Program.cs`), so users land on Browse Courses either way.

**Rationale**: Spec FR-001 says the brand "MUST navigate to the Home page (the
site root)". Targeting `/` is the literal spec interpretation and stays correct
if the Home content ever changes to a dedicated page — only the redirect would
change. The cost is a single 302 hop, which is imperceptible and matches how
browsers already handle root visits.

**Alternatives considered**:
- `href="/Courses"` or `asp-page="/Courses/Index"`: one fewer redirect, but
  hard-codes the current Home content into the brand; if Home ever becomes its
  own page the brand silently points at the wrong place.
- Razor `asp-page` tag helper: cannot target the root, which is a minimal API
  endpoint, not a Razor page.

## 2. Markup Change in `_Layout.cshtml`

**Decision**: Replace `<span class="brand">Libre LMS</span>` with
`<a href="/" class="brand">Libre LMS</a>`. The brand sits in the shared navbar
**outside** the `@if (User.Identity?.IsAuthenticated == true)` / `else` split,
so a single edit renders the link for signed-out and signed-in users alike —
no duplicated markup needed.

**Rationale**: Minimal diff, valid HTML, and the existing `.navbar .brand`
rules (font, size, color `var(--page-bg)`, `white-space: nowrap`) all carry
over unchanged.

**Alternatives considered**:
- Duplicate the brand link inside each auth branch: rejected — needless
  duplication for zero behavioral difference.
- Add an `id` or new CSS class (e.g., `.brand-link`): rejected — reusing
  `.brand` keeps selectors and any future test locators stable.

## 3. Anchor Styling in `site.css`

**Decision**: Add two rules to the existing `.navbar .brand` block/area:
- `text-decoration: none;` — there is no global `a` rule in `site.css`; without
  this, the UA default underline appears on the brand the moment it becomes an
  anchor, breaking the wordmark look.
- `.navbar .brand:hover { color: #ffffff; }` — hover affordance identical in
  spirit to the existing `.navbar .nav-links a.nav-link:hover` rule (which sets
  `color: #ffffff`), satisfying FR-004 (visually identifiable as interactive)
  while staying consistent with the nav's visual language.

**Rationale**: Anchors already get `cursor: pointer` from the UA; the only
missing affordance is hover feedback. Matching the nav-link hover color keeps
the navbar visually coherent (FR-007).

**Alternatives considered**:
- Underline on hover: rejected — inconsistent with nav links, which use color
  change, not underline.
- New CSS custom property for the hover color: rejected — the existing
  hover rule uses the literal `#ffffff`; introducing a variable for one extra
  use adds abstraction without payoff (Principle II).

## 4. JavaScript Interference

**Decision**: No JS changes. Verified that none of the inline scripts in
`_Layout.cshtml` (active-link detection, role switcher, hamburger toggle,
account dropdown) reference `.brand` or `span.brand`. Brand navigation is a
full page load, which resets all client-side nav state (hamburger open,
dropdown open, role toggle) by definition — FR-007's "no stuck nav state"
requirement holds without any extra code.

**Rationale**: Adding JS to "close menus on brand click" would be dead code —
the page unloads first.

**Alternatives considered**:
- Add a click handler to close the hamburger/dropdown before navigating:
  rejected — unnecessary; full page navigation already resets state.

## 5. E2E Test Strategy (Principle XIII)

**Decision**: New spec file `tests/Playwright.Tests/tests/11-brand-home-link.spec.ts`
(following the existing `NN-name.spec.ts` convention and the
`utils/testUsers` credential fixtures). Cover:

1. Signed-out user on Login page: brand is an anchor to `/`; clicking lands on
   Browse Courses in one click, no prompts (US1, SC-001).
2. Signed-out user on Home: clicking the brand stays on Browse Courses, no
   error (edge case: brand on Home).
3. Signed-in learner on My Courses: brand click lands on Browse Courses (US2).
4. Signed-in admin with Admin role view active on the admin Dashboard: brand
   click lands on Browse Courses, not the admin Dashboard (US2, FR-006).
5. Mobile viewport (375px) on the Login page: brand visible and clickable,
   lands on Browse Courses (FR-005).
6. Access-denied variant of the Login page (signed-in learner hitting an admin
   URL): brand present and navigates to Home (edge case).

**Rationale**: Constitution Principle XIII requires E2E proof of the change;
spec 022 established Playwright as the project's verification vehicle. Each
test maps to an acceptance scenario in the spec.

**Alternatives considered**:
- Extend `01-auth.spec.ts`: rejected — brand-link behavior is a distinct
  navigation concern; a dedicated file keeps the suite organized and lets the
  change be verified in isolation.

## 6. Home = Browse Courses (US3)

**Decision**: No implementation work. The root redirect already exists
(`Program.cs` line ~732). The spec pins this as a tested requirement; the new
Playwright spec asserts that hitting `/` shows Browse Courses for anonymous
and signed-in users, locking it against regression (SC-003).

**Rationale**: The value of US3 is verification, not code.

## Open Questions

None — all NEEDS CLARIFICATION candidates from the spec were resolvable from
the spec text and the current codebase state.
