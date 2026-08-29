# Feature Specification: Per-User Theme Preference (System / Light / Dark)

**Feature Branch**: `story/042-user-theme-preference`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2026-08-29

**Status**: Draft

**Input**: User description: "Lets finalize the Settings > Theme functionality for each profile of user. It should persist as long as the user is logged in. i like the System, Light, and Dark option. System is default, light should not be just white but something closer to a paper thats easy to the eyes. Dark should be a good balance of contrast for night reading"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose a theme that applies immediately and persists (Priority: P1)

A signed-in user opens Settings and sees the Theme selector with exactly three options:
System, Light, and Dark. System is the default for new accounts. When the user picks a
theme, the whole application switches to that appearance immediately — no manual reload —
and the choice stays in effect on every page they navigate to, after they close and reopen
the browser, and after they sign out and sign back in. The choice belongs to the user's
account, not the device.

**Why this priority**: This is the heart of the feature. Without it, the Theme selector is
a setting that does nothing — the user explicitly wants the choice to persist for their
profile. Palette quality (Stories 2–3) and live system tracking (Story 4) build on top of
this.

**Independent Test**: Sign in, open Settings, select Dark, and confirm the page re-skins
immediately. Navigate to a course page and confirm it renders dark. Close the browser, sign
in again, and confirm Dark is restored from the account without re-selecting.

**Acceptance Scenarios**:

1. **Given** I am signed in with default settings, **When** I open Settings, **Then** the
   Theme selector shows System, Light, and Dark, with System selected.
2. **Given** I am on any page of the app, **When** I change the theme in Settings, **Then**
   the new appearance takes effect across the page within one second, without a manual
   reload.
3. **Given** I saved the Dark theme, **When** I navigate to any other page of the app,
   **Then** that page renders in Dark.
4. **Given** I saved the Dark theme, **When** I sign out, close the browser, and sign back
   in, **Then** the app renders in Dark without me re-selecting it.
5. **Given** I am signed in, **When** I save a theme change, **Then** I see a confirmation
   that settings were saved; if the save fails, I see a clear error and the theme currently
   displayed is unchanged.

---

### User Story 2 - A light theme that is easy on the eyes (Priority: P2)

The Light theme is not a stark, pure-white page: the background and page surfaces use a
warm, paper-like tone that is comfortable for long reading sessions. All text remains
comfortably readable, and buttons, badges, alerts, and form controls keep their meaning
and visibility.

**Why this priority**: The user explicitly asked for a paper-like light rather than plain
white. If the Light palette stayed white, the feature would not meet the request even
though the plumbing works.

**Independent Test**: Switch to Light and confirm page background and card surfaces are a
warm paper tone rather than pure white. Read body text on the main page types (catalog,
course detail, My Courses, Settings, profile) and confirm it is comfortably legible.

**Acceptance Scenarios**:

1. **Given** the Light theme is active, **When** I view any page, **Then** the background
   and card surfaces are a warm paper-like tone, not pure white.
2. **Given** the Light theme is active, **When** I read body or secondary text on any
   page, **Then** it is clearly legible, meeting WCAG AA contrast (4.5:1 or better for
   normal-size text).
3. **Given** the Light theme is active, **When** I view buttons, badges, alerts, and form
   controls, **Then** each retains the same meaning and visibility as in the current
   default appearance.

---

### User Story 3 - A dark theme balanced for night reading (Priority: P2)

The Dark theme is a comfortable, balanced dark: a soft dark background (not harsh pure
black), text light enough for long reading without eye strain, and all controls, borders,
and status colors adjusted so they stay distinguishable.

**Why this priority**: The user explicitly asked for a good balance of contrast for night
reading. A naive inversion (white on pure black) would miss the request and be tiring to
read.

**Independent Test**: Switch to Dark and confirm the background is a soft dark tone rather
than pure black. Confirm body text, secondary text, borders, buttons, and alerts stay
distinguishable on the main page types.

**Acceptance Scenarios**:

1. **Given** the Dark theme is active, **When** I view any page, **Then** the background is
   a soft dark tone (not pure black) and body text is comfortably readable.
2. **Given** the Dark theme is active, **When** I view body and secondary text, **Then**
   both meet WCAG AA contrast (4.5:1 or better for normal-size text) against their
   backgrounds.
3. **Given** the Dark theme is active, **When** I view buttons, badges, alerts, and form
   controls, **Then** each remains visually distinguishable from its surroundings and keeps
   its semantic color (success vs. error).

---

### User Story 4 - System mode follows my device, with no flash of the wrong theme (Priority: P3)

Users on System (the default) get an app appearance that matches their device's light/dark
setting. If they flip their device setting while the app is open, the app follows
automatically — no reload needed. When any page loads, the correct theme is in effect from
the first paint, so nobody ever sees a flash of the wrong theme. Visitors who are not
signed in also get the System theme.

**Why this priority**: System is the default for everyone, so its behavior (including
no-flash) affects all users on every load — but the feature delivers its core value
through Stories 1–3, and a static System mapping already covers the basic case.

**Independent Test**: Sign in with System selected and confirm the app matches the browser
device setting. Flip the device setting while a page is open and confirm the app follows.
Reload a page and confirm the theme is correct before anything is painted (no flash).

**Acceptance Scenarios**:

1. **Given** my device setting is dark and my theme is System, **When** I open the app,
   **Then** the app appears dark.
2. **Given** the app is open with System selected, **When** I change my device light/dark
   setting, **Then** the app appearance follows automatically within one second, without a
   reload.
3. **Given** any saved theme, **When** I load any page, **Then** the correct theme is in
   effect from the first paint, with no visible flash of a different theme.
4. **Given** I am not signed in, **When** I browse the app, **Then** I see the System
   theme following my device setting.

---

### Edge Cases

- **Device setting changes while the app is open in System mode** → the app follows
  immediately, without a reload.
- **Anonymous (not signed in) visitors** → the System theme applies; no preference is
  created or modified for anonymous visitors.
- **Sign out, then back in** → the saved preference is restored from the account; it
  survives clearing the browser and works on a different device.
- **Invalid or unrecognized stored value** (e.g. data corruption) → the app falls back to
  System, and the Settings page shows System as the selected option.
- **Failed save** (server error) → a clear error message is shown on the Settings page and
  the currently displayed theme is unchanged until a save succeeds.
- **Changing the theme while a form on the page has unsaved input** (e.g. an in-progress
  profile edit) → the theme change must not discard the in-progress form input.
- **SCORM course content** → the application chrome around authored content (navigation,
  header, footer) is themed, but the third-party authored content itself keeps its own
  appearance so authored material is not broken by theming.
- **Responsive/mobile layouts** → all three themes work with the responsive layouts; no
  layout regressions appear in any theme.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST offer exactly three theme options — System, Light, and Dark —
  in the Settings page Theme control, with System as the default for new accounts.
- **FR-002**: The system MUST persist the selected theme on the user's account and restore
  it on every page while the user is signed in, and after later sign-ins.
- **FR-003**: The system MUST apply a theme change immediately upon selection, without a
  full-page reload and without discarding in-progress form input on the current page.
- **FR-004**: The Light theme MUST render backgrounds and page surfaces in a warm,
  paper-like tone (not pure white) and MUST keep body text at WCAG AA contrast (4.5:1 or
  better for normal-size text).
- **FR-005**: The Dark theme MUST render backgrounds in a soft dark tone (not pure black)
  and MUST keep body and secondary text at WCAG AA contrast, with buttons, badges, alerts,
  and form controls remaining distinguishable and semantically colored.
- **FR-006**: The system MUST apply the active theme consistently across all application
  pages: public catalog, course pages, My Courses, profile, Settings, admin pages, and the
  SCORM shell chrome.
- **FR-007**: When System is selected, the app MUST follow the user's device light/dark
  setting and MUST follow changes to that setting while the app is open, without a reload.
- **FR-008**: On every page load, the active theme MUST be in effect from the first paint —
  no visible flash of a different theme.
- **FR-009**: Users who are not signed in MUST see the System theme; the system MUST NOT
  create or modify account preferences for anonymous visitors.
- **FR-010**: If a stored theme value is missing, invalid, or unrecognized, the system MUST
  fall back to System, and the Settings page MUST present System as selected.
- **FR-011**: If saving a theme fails, the system MUST show a clear error message and the
  currently displayed theme MUST remain unchanged.
- **FR-012**: Third-party authored SCORM course content MUST keep its own appearance; only
  the application chrome around it is themed.

### Key Entities

- **User theme preference**: An attribute of a user's account — the chosen value (System,
  Light, or Dark) set in Settings. Defaults to System. Belongs to the account, not to a
  device or browser.
- **Theme appearance (palette)**: The complete visual definition of an appearance —
  background, surfaces, primary and secondary text, borders, brand/accent colors, and
  semantic colors (success, error, category badges). Two appearances are defined: Light
  (paper-like) and Dark (night-reading balanced).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user who changes the theme in Settings sees the new appearance within one
  second, on the same page, without a manual reload.
- **SC-002**: 100% of application pages render in the user's saved theme after sign-in,
  after closing and reopening the browser, and after sign-out/sign-in (verified on the
  standard page set: home/catalog, course detail, My Courses, profile, Settings).
- **SC-003**: In both Light and Dark themes, body and secondary text meet WCAG AA contrast
  (4.5:1 or better) against their backgrounds on all standard page types.
- **SC-004**: 100% of page loads show the correct theme from the first paint — no visible
  flash of a different theme (verified in the browser with both Light and Dark saved).
- **SC-005**: A user on System who changes their device setting sees the app follow
  automatically within one second, without a reload.
- **SC-006**: In the Light theme, no standard page uses a pure-white background or surface;
  all use a warm paper-like tone.

## Assumptions

- The existing Settings page Theme selector and its account-level persistence are the
  foundation of this feature; the spec completes the missing part — actually applying the
  saved theme to the application UI.
- "Persist as long as the user is logged in" is interpreted as: the preference belongs to
  the user's account, applies on every page while signed in, and is restored on later
  sign-ins. It is not a device-local browser setting (which would be lost on browser
  clearing or a different device).
- The theme applies to the application's own UI. Authored third-party SCORM content is not
  restyled; restyling authored material could break authoring-tool output and is out of
  scope.
- WCAG AA (4.5:1 for normal text) is the contrast target for both themes; it is the
  industry-standard accessibility floor for readable body text.
- The application runs in modern browsers that expose the device light/dark setting; a
  browser without that capability treats System as Light.
- The Email Notifications control on the same Settings page is unchanged by this feature.
- No per-page or per-section theme overrides are in scope; the theme is one global
  account-level setting.
