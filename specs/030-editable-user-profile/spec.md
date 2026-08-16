# Feature Specification: Editable User Profile With Photo & Course History

**Feature Branch**: `story/030-editable-user-profile`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2026-08-16

**Status**: Ready for Planning

**Input**: User description: "the View Profile should be editable for users (logged in) and can edit Name (must have verification email to apply changes). Profile should also display courses they enrolled and completed. user should be able to edit display photo and display photo should be visible near the Profile name at the upper right nav menu (if not admin)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Edit My Display Name (Priority: P1)

A signed-in user opens their View Profile page, which now offers editing. They can change
their display name and save. The change is applied only if their account has a verified
email address. Users whose email is not yet verified see a clear explanation and are given
a way to verify, and their name change is not applied until verification is complete.
Once applied, the new name is visible everywhere the name currently appears — on the
profile page and next to the account entry at the upper right of the navigation menu.

**Why this priority**: Name editing is the primary "editable profile" capability the user
asked for, and the email-verification gate is the trust/identity anchor for it. Without
this story the feature does not exist.

**Independent Test**: Sign in with a verified account, change the name on the profile page,
save, and confirm the new name appears on the profile and in the upper-right nav menu.
Repeat with an unverified account and confirm the change is refused with an actionable
message. No other part of the feature is required for either test.

**Acceptance Scenarios**:

1. **Given** a signed-in user with a verified email, **When** they open View Profile,
   **Then** they see their current name in an editable field along with their email, role,
   and (if set) display photo.
2. **Given** a signed-in user with a verified email, **When** they enter a new non-empty
   name and save, **Then** the change is applied, a success confirmation is shown, and the
   new name appears in the upper-right nav menu without requiring a page reload beyond the
   normal save response.
3. **Given** a signed-in user whose email is not verified, **When** they try to save a new
   name, **Then** the change is not applied and they see a message explaining that a
   verified email is required, with a visible way to verify (or request a new
   verification link).
4. **Given** a signed-in user whose email is not verified, **When** they verify their email
   and then save the name change, **Then** the change is applied successfully.
5. **Given** a signed-in user, **When** they submit a name that is blank/whitespace-only or
   longer than 100 characters, **Then** the submission is rejected with a field-level
   validation message and nothing is saved.

---

### User Story 2 - See Enrolled & Completed Courses on My Profile (Priority: P1)

A signed-in user opens View Profile and, in addition to their personal details, sees a
"My Courses" area that lists every course they are enrolled in, split into two groups:
courses still in progress ("Enrolled") and courses they have completed ("Completed").
Each completed course is recognizable at a glance. A user with no enrollments sees a
friendly empty state instead of a blank section.

**Why this priority**: This turns the profile into a personal record of learning — the
second core capability requested. It is independent of name editing and photo work.

**Independent Test**: Sign in as a user who has at least one active enrollment and at
least one course with a finished attempt; open View Profile and confirm both courses
appear in the correct groups with correct titles and status labels. No name or photo
changes are needed for this test.

**Acceptance Scenarios**:

1. **Given** a signed-in user enrolled in courses, some with finished attempts and some
   without, **When** they open View Profile, **Then** the profile lists all enrolled
   courses grouped into "Enrolled" (not yet completed) and "Completed" sections with each
   course's title and a readable status label.
2. **Given** a signed-in user, **When** a course's most recent attempt was finished
   successfully, **Then** the course appears in the "Completed" group, not "Enrolled".
3. **Given** a signed-in user with no enrollments, **When** they open View Profile,
   **Then** the courses area shows a clear empty state (e.g., "You haven't enrolled in any
   courses yet") rather than an error or blank space.
4. **Given** a signed-in user, **When** the enrollment or completion data cannot be loaded,
   **Then** the profile still renders their personal details and the courses area shows a
   friendly error message instead of breaking the page.

---

### User Story 3 - Edit My Display Photo & See It in the Nav Menu (Priority: P2)

A signed-in user can upload (or replace) a display photo from their profile page using a
standard image picker. The photo is saved to their account and displayed on the profile.
The photo also appears in the upper-right navigation menu next to the profile name, so
other visitors and the user themselves can recognize them at a glance. Users without a
photo get a sensible placeholder (e.g., initials or a generic avatar) in both places.

**Why this priority**: Valuable and explicitly requested, but additive — the profile is
fully useful for name editing and course history before photo support exists.

**Independent Test**: Sign in, upload a photo on the profile page, confirm it appears on
the profile and in the upper-right nav menu for the audiences defined in FR-008, then
upload a replacement and confirm the new photo replaces the old one.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they choose a valid image file and save, **Then**
   the photo is saved to their profile and displayed on the profile page and in the
   upper-right nav menu (for the audiences in FR-008).
2. **Given** a signed-in user with an existing photo, **When** they upload a new image,
   **Then** the new photo replaces the old one everywhere it is shown.
3. **Given** a signed-in user, **When** they choose a file that is not an image or exceeds
   the size limit, **Then** the upload is rejected with a clear message and their existing
   photo (or placeholder) is unchanged.
4. **Given** a signed-in user with no photo set, **When** they view the profile and the
   upper-right nav menu, **Then** a placeholder avatar is shown in both places, not a
   broken image icon.
5. **Given** a signed-in user with an admin role and a display photo, **When** the
   navigation is in the Admin view, **Then** no photo is shown next to the name; **When**
   they switch to the Learner view, **Then** the photo is shown next to the name again.
   Users without an admin role always see the photo (or placeholder) next to the name.

---

### Edge Cases

- **Unverified email + name edit**: change is refused with an actionable message (verify
  now / resend link); the profile page still renders normally with all other data.
- **Verification link expired** when an unverified user tries to verify: they can request
  a new link; name editing remains blocked until verification succeeds.
- **Name unchanged**: saving the same name succeeds as a no-op (no error).
- **Concurrent name edits** (two tabs): the last save wins; no corrupt or partial names.
- **Photo upload while signed out or via direct request without a session**: rejected.
- **Oversized or non-image file**: rejected before anything is saved; previous photo kept.
- **Photo file that is a valid image container but corrupt**: treated as invalid; user is
  told the file could not be read; previous photo kept.
- **Retaken completed course**: if a previously completed course is started again (new
  in-progress attempt), the course still appears under "Completed" as long as a completed
  attempt exists, so completion is never lost from the record.
- **Course with only abandoned/failed attempts**: appears under "Enrolled" with its
  current status label — it is not counted as completed.
- **Admin users**: admins can edit their own name and photo through the same self-service
  profile page; the nav-menu photo audience follows FR-008.
- **Anonymous visitor** requests the profile page: redirected to sign in (no data exposed).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow any signed-in user to open their own View Profile
  page and edit their display name; no other role or permission is required.
- **FR-002**: The system MUST apply a display-name change only when the user's account has
  a verified email address; for unverified accounts the change MUST be refused with a
  message explaining the requirement and a visible way to complete verification.
- **FR-003**: The system MUST validate display names: non-empty after trimming, at most
  100 characters, and no line breaks; invalid values MUST be rejected with a field-level
  message and nothing persisted.
- **FR-004**: After a successful name change, the new name MUST be shown on the profile
  page and in the upper-right navigation menu account entry for the user's next rendered
  page.
- **FR-005**: The profile page MUST display a "My Courses" area listing every course the
  user is enrolled in, grouped into "Enrolled" (no completed attempt yet) and "Completed"
  (at least one completed attempt).
- **FR-006**: A course MUST be considered completed for profile display when the user has
  an attempt whose status is "completed" or "passed"; all other enrollments (in-progress,
  abandoned, failed, or no attempt) belong to "Enrolled" with a readable status label.
- **FR-007**: Each listed course MUST show its title and status label; the "Completed"
  group MUST be visually distinguishable from the "Enrolled" group.
- **FR-008**: The display photo MUST be visible near the profile name at the upper-right
  navigation menu for all signed-in users. For users with an admin role, the photo MUST be
  shown only while the navigation is in the Learner view and MUST be hidden when the role
  toggle is set to the Admin view; users without an admin role MUST always see it (or the
  FR-011 placeholder). The profile page itself MUST show the photo regardless of role or
  view.
- **FR-009**: The system MUST allow any signed-in user to upload and replace their own
  display photo via a standard image picker on the profile page.
- **FR-010**: The system MUST accept only image files (JPEG, PNG, WebP, or GIF) up to 5 MB
  and reject anything else with a user-friendly message without altering the stored photo.
- **FR-011**: When a user has no display photo, the system MUST show a placeholder avatar
  (e.g., initials or generic icon) on the profile page and in the upper-right nav menu
  instead of a broken image.
- **FR-012**: The profile page MUST continue to show the user's email and role as
  read-only information; email address itself is NOT editable in this feature.
- **FR-013**: Unauthenticated visitors requesting the profile page MUST be redirected to
  the sign-in page.
- **FR-014**: If course data cannot be loaded, the profile page MUST still render the
  user's personal details and show a friendly error in the courses area only.

### Key Entities *(include if feature involves data)*

- **User Profile**: the signed-in learner's record — display name (editable, gated by
  email verification), email (read-only here, with verified/unverified state), role, and
  an optional display photo. One profile per user; the photo and name are personal, not
  organization-scoped.
- **Email Verification State**: the account-level flag distinguishing verified from
  unverified email addresses (self-service sign-ups start unverified; admin-created and
  seeded accounts are verified). Drives whether name changes may be applied.
- **Display Photo**: an image belonging to a user, shown as a small avatar on the profile
  and in the navigation menu; replaceable at any time; absence falls back to a
  placeholder.
- **Enrollment**: the relationship between a user and a course, with the time of
  enrollment; the source of the "Enrolled" list on the profile.
- **Course Attempt**: a user's work on a course — status (in-progress, completed,
  abandoned, passed, failed), score, and timestamps. Determines the "Completed" grouping
  and the per-course status label.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A signed-in user with a verified email can change their display name in no
  more than 30 seconds (open profile → type → save) and sees the new name in the
  upper-right nav menu on the resulting page.
- **SC-002**: 100% of name-change attempts by unverified users are blocked with a clear,
  actionable message (verified email required + how to verify); zero unverified name
  changes are persisted.
- **SC-003**: A user with 10 or fewer enrollments sees their complete, correctly grouped
  course list on the profile within 2 seconds of the page load; grouping (Enrolled vs
  Completed) is correct for 100% of courses in test data covering all attempt statuses.
- **SC-004**: A user can upload a valid display photo in under 30 seconds and see it
  rendered on the profile page and in the upper-right nav menu (for the FR-008 audience)
  on their next page view, without any manual intervention.
- **SC-005**: At least 90% of users complete a name or photo update on their first attempt
  without needing help, and every rejected attempt (validation or verification gate) is
  accompanied by a message the user says tells them exactly what to do next.

## Assumptions

- **Verification gate semantics**: "must have verification email to apply changes" means
  the account-level email-verified state must be true before a name change is applied
  (an existing platform state: self-service sign-ups start unverified, admin-created and
  seeded accounts are verified). It does NOT mean a fresh verification email is sent for
  every individual change; no per-change re-verification flow is in scope.
- **Completed definition**: a course counts as "Completed" when the user has at least one
  attempt with status "completed" or "passed". Retakes that leave an older completed
  attempt behind do not remove the course from the Completed group.
- **Course list scope**: the profile shows all of the user's enrollments, consistent with
  the existing My Courses page (not filtered to one organization).
- **Photo constraints**: JPEG/PNG/WebP/GIF, max 5 MB, displayed as a small circular avatar
  near the name; stored per user and replaceable. Exact storage mechanics are a planning
  concern, not a spec concern.
- **Self-service only**: users edit only their own profile. Editing other learners' names
  or photos through the admin management pages is out of scope for this feature (admin
  learner management already exists separately).
- **Read-only fields**: email and role remain display-only on the profile; changing the
  email address is a different feature.
- **Placeholder default**: users created without a photo (the current state of all
  accounts) get an initials/generic placeholder until they upload one.
- **Nav photo audience (resolved Q1)**: all signed-in users see the photo next to the name
  in the upper-right nav; admin-role users only in the Learner view of the nav, never in
  the Admin view.
- **Existing pages reused**: the current "View Profile" page becomes the editable profile
  page; no separate edit page is introduced.
