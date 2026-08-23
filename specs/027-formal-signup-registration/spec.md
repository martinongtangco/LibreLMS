# Feature Specification: Formal Signup & Registration

**Feature Branch**: `story/027-formal-signup-registration`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2026-08-15

**Status**: Complete (merged 2026-08-15)

**Input**: User description: "formalize Signup and Registration. Theres still a Demo credentials hint 'Demo credentials: alice@example.com / password123 (student) or admin@example.com / password123 (admin)' which we should also remove. Sign ups should be email unique. Passwords strict. There should also be a forgot password. We need to also mock email sending for verification and signup. I plan to use SendGrid in the future."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Self-Service Account Creation (Priority: P1)

A new user can create their own learner account directly from the site without asking an
administrator. They provide a full name, an email address, and a password that meets the
strict password policy. The account is created, a verification email and a welcome email
are generated for the address, and the user is told to check their email.

**Why this priority**: This is the core of "formalizing signup and registration" — today
there is no self-service path at all (accounts only exist because an admin creates them or
the system seeds them). Without it, no other part of this feature (verification, forgot
password, email cleanup) has anything to act on.

**Independent Test**: Can be fully tested by submitting a valid sign-up form and confirming
that (a) an account exists in an unverified state, (b) a duplicate email is rejected, and
(c) any policy-violating password is rejected with a specific reason. Delivers a working
registration path on its own.

**Acceptance Scenarios**:

1. **Given** a visitor who is not signed in, **When** they submit the sign-up form with a
   full name, a valid email that is not registered, a password meeting the strict policy,
   and a matching password confirmation, **Then** an account is created in an unverified
   state, a verification email and a welcome email are generated for that address, and a
   confirmation screen tells the user to check their email.
2. **Given** an email address that is already registered (in any letter case), **When** the
   visitor submits the sign-up form with it, **Then** the submission is rejected with a
   clear "email already in use" message and no duplicate account is created.
3. **Given** a password that violates any rule of the strict password policy (too short,
   missing a required character class, on the common-password blocklist), **When** the
   visitor submits it, **Then** it is rejected with a specific message identifying which
   rule(s) it failed.
4. **Given** a password that contains the visitor's full name or email address, **When** the
   visitor submits it, **Then** it is rejected with a specific message.
5. **Given** a password and password confirmation that do not match, **When** the visitor
   submits the form, **Then** it is rejected with a clear "passwords do not match" message.
6. **Given** a malformed email address, **When** the visitor submits the form, **Then** it
   is rejected with a clear format-validation message.
7. **Given** a visitor who repeatedly submits sign-ups for the same email within a short
   time window beyond the allowed threshold, **When** they submit again, **Then** further
   attempts are throttled with a friendly "try again later" message.

---

### User Story 2 - Email Verification (Priority: P1)

A user who just signed in on the verification link in their email activates their account.
Until this happens, they cannot sign in, and the sign-in screen offers to resend the
verification email. Links are single-use and expire after 24 hours.

**Why this priority**: Verification is what makes the registration "formal" — it confirms
the user actually owns the email they registered with, which is the foundation of the
forgot-password flow (recovery depends on the email being trustworthy).

**Independent Test**: Can be fully tested by creating an account, opening the verification
link from the developer-observable mock email, confirming the account becomes sign-in
able, and confirming a second use of the same link (or an expired one) is rejected.

**Acceptance Scenarios**:

1. **Given** an unverified account, **When** the user opens the verification link from the
   email, **Then** the account becomes verified and the user can sign in.
2. **Given** a verification link that has already been used, **When** it is opened again,
   **Then** it is rejected as already used and the user is offered the option to request a
   new link.
3. **Given** a verification link older than 24 hours, **When** it is opened, **Then** it is
   rejected as expired and the user is offered the option to request a new link.
4. **Given** an unverified account, **When** the user attempts to sign in, **Then** sign-in
   is blocked with a "please verify your email" message and an option to resend the
   verification email.
5. **Given** a verification link that is malformed, tampered with, or does not match any
   account, **When** it is opened, **Then** it is rejected with an error and no account
   state changes.

---

### User Story 3 - Password Recovery (Forgot Password) (Priority: P1)

A user who has forgotten their password can request a reset from the sign-in screen by
entering their registered email. A reset email with a single-use link (valid for 30
minutes) is generated. Following the link lets them choose a new password that meets the
strict policy. After a successful reset, all of their existing sessions are invalidated
and they sign in again with the new password. The on-screen response to a reset request is
the same whether or not the email is registered, so the form cannot be used to discover
which emails have accounts.

**Why this priority**: A formal registration lifecycle without self-service recovery would
force users to contact an administrator for every forgotten password — the exact problem
this feature removes.

**Independent Test**: Can be fully tested by requesting a reset for a known email, following
the link from the developer-observable mock email, setting a new password, confirming old
sessions no longer work, and confirming used/expired links and unregistered emails are
handled without leaking account existence.

**Acceptance Scenarios**:

1. **Given** a registered email address, **When** a signed-out user submits it in the
   forgot-password form, **Then** a reset email with a single-use link is generated for that
   address and a neutral confirmation ("if an account exists for that address, a reset link
   was sent") is shown.
2. **Given** an email address that has no account, **When** a signed-out user submits it in
   the forgot-password form, **Then** the same neutral confirmation is shown, no email is
   generated, and the response is indistinguishable from the registered case.
3. **Given** a valid, unexpired reset link, **When** the user follows it and submits a new
   password meeting the strict policy with a matching confirmation, **Then** the password
   is updated, the link is consumed, and the user must sign in again with the new password.
4. **Given** a reset link that has already been used or is older than 30 minutes, **When**
   it is followed, **Then** it is rejected as used or expired and the user is offered the
   option to request a new reset.
5. **Given** an account that completed a password reset while holding active sessions,
   **When** any of those existing sessions is used, **Then** it is rejected and the user
   must sign in again.
6. **Given** repeated reset requests for the same email within a short time window beyond
   the allowed threshold, **When** the user submits another request, **Then** it is
   throttled with a friendly "try again later" message.

---

### User Story 4 - Login Page Cleanup (Priority: P2)

The sign-in screen no longer displays demo account credentials. Instead it presents the
normal, production-shaped entry points: sign in, create an account, and forgot password.

**Why this priority**: Displaying working credentials on a public page is a security and
trust problem, but it does not block any user journey — it is a cleanup that ships with
the formalized flows.

**Independent Test**: Can be fully tested by loading the sign-in screen and confirming no
demo/test credentials appear anywhere on the page and that the sign-up and forgot-password
links are present and lead to the right places.

**Acceptance Scenarios**:

1. **Given** the sign-in screen, **When** it is rendered for any visitor, **Then** no demo
   or test account credentials are displayed anywhere on the page.
2. **Given** the sign-in screen, **When** it is rendered, **Then** it offers a clear link to
   the sign-up page and a clear link to the forgot-password flow.

---

### Edge Cases

- **Email case variants**: `Alice@Example.com` and `alice@example.com` are the same address
  for every purpose — sign-up uniqueness, sign-in lookup, verification, and reset.
- **Concurrent duplicate sign-ups**: if two sign-up submissions for the same email arrive
  nearly simultaneously, exactly one account is created and the other receives the
  "already in use" error.
- **Links from another device**: a verification or reset link opened in a different
  browser or device still works — links are bound to the account, not to a session.
- **Deleted or nonexistent accounts**: reset or verification requests for an address with
  no account behave exactly like the expired/invalid cases, leaking no information.
- **Resend while a link is pending**: resending a verification email issues a new link that
  supersedes the previous one; no second account is ever created for the same address.
- **Unusual characters in passwords**: passwords containing Unicode or special characters
  are accepted as long as they meet the policy — there is no arbitrary character ban.
- **Mock delivery failure or delay**: a mock email "send" that fails must not block or fail
  the sign-up or reset flow; the failure is recorded and the user always has a resend path.
- **Existing accounts**: accounts that predate this feature (including seeded demo
  accounts) are treated as already verified, so existing sign-in behavior does not change.

## Requirements *(mandatory)*

### Functional Requirements

**Sign-up & registration**

- **FR-001**: System MUST provide a self-service sign-up page, reachable without a prior
  sign-in, that collects a full name, an email address, a password, and a password
  confirmation.
- **FR-002**: System MUST allow at most one account per email address. The uniqueness check
  and all email lookups MUST be case-insensitive, and a duplicate sign-up MUST be rejected
  with a clear "email already in use" message. Concurrent duplicate submissions MUST result
  in exactly one account.
- **FR-003**: System MUST enforce a strict password policy on every user-chosen password
  (sign-up, password reset, and admin-created accounts): at least 12 characters; at least
  one uppercase letter, one lowercase letter, and one digit; must not contain the user's
  full name or email address (case-insensitive); must not appear on a blocklist of
  commonly used passwords.
- **FR-004**: System MUST reject a policy-violating password with a specific message
  identifying which rule(s) it failed, at the point of entry.
- **FR-005**: System MUST validate that the email is well-formed, the full name is present,
  and the password confirmation matches the password, rejecting each failure with its own
  clear message.
- **FR-006**: System MUST store credentials in a non-recoverable form (salted one-way) and
  MUST never display, log, or return a user's password in any form.
- **FR-007**: Accounts created through self-service sign-up MUST be assigned the default
  learner role and the platform's default organization; privileged roles remain
  administrator-assigned only.
- **FR-008**: On successful sign-up, System MUST generate (a) a verification email
  containing a single-use verification link and (b) a welcome email notifying the user that
  their account was created, both delivered through the email boundary in FR-019.
- **FR-009**: After a successful sign-up, System MUST show a confirmation that the account
  was created and a verification email was sent; the user MUST NOT be signed in
  automatically.
- **FR-010**: System MUST rate-limit sign-up attempts per email address to prevent mass
  account creation and abuse.

**Email verification**

- **FR-011**: Accounts created through self-service sign-up MUST start in an unverified
  state. Sign-in attempts for an unverified account MUST be blocked with a message
  prompting verification and an option to resend the verification email.
- **FR-012**: Verification links MUST be single-use and MUST expire after 24 hours. A used,
  expired, or invalid link MUST produce a clear error with the option to request a new
  link.
- **FR-013**: System MUST allow resending a verification email for an existing unverified
  account from the sign-in screen, and MUST rate-limit these resends to prevent spam.

**Forgot password**

- **FR-014**: System MUST provide a forgot-password flow in which a signed-out user submits
  their registered email and receives a reset email containing a single-use reset link.
- **FR-015**: The on-screen response to a reset request MUST be identical whether or not
  the submitted email has an account, so the flow cannot be used to discover which emails
  are registered.
- **FR-016**: Reset links MUST be single-use and MUST expire within 30 minutes. A used,
  expired, or invalid link MUST produce a clear error with the option to request a new
  reset.
- **FR-017**: On a successful password reset, the new password MUST meet the strict policy
  (FR-003), the reset link MUST be consumed, and all existing sessions for that account
  MUST be invalidated so the user must sign in again.
- **FR-018**: System MUST rate-limit reset requests per email address to prevent abuse.

**Email delivery (mock)**

- **FR-019**: All transactional email sending MUST go through a single, swappable email
  delivery boundary so that the current implementation can be replaced with a production
  email provider without changing sign-up, verification, or reset logic.
- **FR-020**: The current mock email implementation MUST record every email it "sends"
  (recipient, purpose, subject, body, time sent) in a developer-observable way (application
  logs and/or a developer-accessible outbox) so that verification and reset links can be
  found and used without a real mailbox.
- **FR-021**: The current implementation MUST send zero real outbound emails.
- **FR-022**: Email "sending" MUST be non-blocking for the sign-up, verification, and reset
  flows: a failed or delayed mock send MUST NOT fail the user's action; failures MUST be
  recorded, and the user MUST always have a resend path for the link they need.

**Sign-in screen & security hygiene**

- **FR-023**: The sign-in screen MUST NOT display demo or test account credentials.
- **FR-024**: The sign-in screen MUST offer clear links to the sign-up page and the
  forgot-password flow.
- **FR-025**: Failed sign-in attempts MUST return a generic "invalid email or password"
  message that does not reveal whether the submitted email is registered.
- **FR-026**: Email addresses MUST be stored and compared in a normalized form so
  uniqueness and lookups are case-insensitive across sign-up, sign-in, verification, and
  reset.

### Key Entities

- **Account**: A person who uses the platform. Key attributes: full name, email (unique,
  normalized), credential (non-recoverable), role (default: learner), organization
  (default: platform's default organization), verification status (unverified/verified),
  creation time. Relationships: belongs to one organization; has zero or more course
  enrollments; has at most one pending verification token and one pending reset token.
- **Verification token**: A single-use, time-limited (24 hours) reference that links an
  account to its verification email. Consumed when used; superseded when a new
  verification email is requested.
- **Password reset token**: A single-use, time-limited (30 minutes) reference that links an
  account to its reset email. Consumed when used; a new reset request supersedes any prior
  pending token for that account.
- **Outbox email record (mock)**: A developer-time record of a transactional email produced
  by the mock provider: recipient, purpose (verification / welcome / reset), subject,
  body, time sent, and status.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user can go from first arriving at the site to being signed in —
  including email verification — in under 3 minutes using only the interface and the
  developer-observable mock email.
- **SC-002**: 100% of duplicate-email sign-up attempts (including concurrent submissions)
  are rejected with a clear error, and zero duplicate accounts are ever created.
- **SC-003**: 100% of policy-violating passwords are rejected across every account-creation
  path (self-service sign-up, password reset, admin-created accounts), and zero
  policy-violating password is accepted anywhere.
- **SC-004**: After a password reset, 100% of that account's pre-existing sessions are
  invalidated (sign-in required again), and zero reused verification or reset link
  succeeds.
- **SC-005**: The sign-in screen renders with zero demo or test credentials visible.
- **SC-006**: All transactional emails (verification, welcome, reset) can be retrieved by a
  developer without a real mailbox, and the verification and reset flows can be completed
  end-to-end using only mock-delivered links.
- **SC-007**: 90% of users complete the forgot-password flow (request → new password →
  signed in) in under 2 minutes without assistance.
- **SC-008**: The system sends zero real outbound emails in its current configuration.

## Assumptions

- Self-service sign-up creates learner accounts only, assigned to the platform's default
  (root) organization. Privileged roles (organization admin, super user) remain
  administrator-assigned; no self-service path for them exists in this slice.
- Sign-in is blocked until email verification completes (the "formal" behavior). All
  pre-existing accounts — including the seeded demo accounts — are treated as already
  verified when this feature is introduced, so existing sign-in workflows and automated
  UI tests keep working.
- The strict password policy defaults chosen here are: minimum 12 characters; at least one
  uppercase letter, one lowercase letter, and one digit; no full name or email address
  inside the password; and rejection of passwords on a blocklist of commonly used
  passwords. Exact thresholds and blocklist source are planning-time decisions.
- Token lifetimes: verification links 24 hours; password-reset links 30 minutes.
- The mock email provider is the only delivery implementation in this slice. Production
  email delivery (e.g., SendGrid, which the user plans to adopt later) is explicitly out of
  scope; the delivery boundary must make that swap a provider/configuration change, not a
  logic change.
- The seeded demo accounts themselves are retained for development and automated UI
  testing; only the visible credentials hint on the sign-in screen is removed.
- The strict password policy and email uniqueness also apply to learner accounts created
  by administrators, so every account-creation path enforces the same rules.
- "Forgot password" is the signed-out recovery flow reachable from the sign-in screen. An
  in-session "change password" capability for signed-in users is out of scope for this
  slice.
- Rate-limiting uses reasonable defaults (a small number of resend/reset attempts per email
  per hour, and a throttle on repeated sign-up attempts per email); exact thresholds are
  planning-time decisions.
- After a successful sign-up, the user sees a confirmation screen and is not signed in
  automatically.
