# Research: Formal Signup & Registration

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-15

Phase 0 output. The Technical Context in [plan.md](plan.md) contained no unresolved
NEEDS CLARIFICATION items — every open question was resolvable from direct codebase
inspection (this repo) plus stable .NET 10 / OWASP practice. Each decision below is
consolidated in the required format: **Decision / Rationale / Alternatives considered**.

The single most important empirical finding:

> **The module-boundary gate is RED on master.** `dotnet test tests/ArchitectureTests`
> fails 2 of 14 tests: Management → Enrollment (violating types: `UserService`,
> `UserInfoLookup`, `DashboardService`, `OrganizationService`, `AdminEnrollmentService`,
> `ManagementSeeder`) and Management → Catalog (violating types: `AdminEnrollmentService`,
> `CourseVisibilityService`, `DashboardService`, `OrganizationService`). Constitution
> Development Workflow requires this gate green before a slice is "done", so this slice
> carries a behavior-preserving remediation workstream (R9).

---

## R1. Module placement for the account lifecycle

**Decision**: Account lifecycle logic (`RegistrationService`, `CredentialPolicy`,
`PasswordHasher`, `EmailThrottle`) lives in **Enrollment/Application** — the module that
owns `Student`. Admin-created accounts route through a new `IUserProvisioning` contract
(Enrollment.Contracts, implemented in Enrollment) so the same policy/hashing core applies
to every creation path. The email seam (`ITransactionalEmailSender` + `OutboundEmail`
record) lives in **SharedKernel**; the mock implementation and dev outbox live in **Host**
(host-level dev concern, next to the Razor pages and cookie auth).

**Rationale**: Constitution I/III — `Student` is Enrollment's domain entity; SharedKernel
is the existing home for cross-cutting primitives (`Entity<T>`, `Result`, `RoleNames`,
`IDomainEvent`); Host is the composition root and is exempt from module-boundary rules.
Management's user management already crosses into Enrollment (the source of the baseline
gate failure) — routing creation through a contract both fixes that and enforces FR-003
on admin-created accounts (spec Assumption 7).

**Alternatives considered**:
- New "Identity" module — rejected: premature scaffolding; Constitution II says a module
  only gets built when a slice needs one, and this slice needs one *service*, not a module.
- Email seam in Enrollment.Contracts — rejected: email is cross-cutting (welcome,
  verification, reset, future notifications); it does not belong to one module.
- Mock email sender inside a module — rejected: the mock is a dev-time observability
  tool, not a domain behavior; it belongs where the app is assembled (Host).

## R2. Password storage

**Decision**: PBKDF2-HMAC-SHA256 via the built-in
`System.Security.Cryptography.KeyDerivation.Pbkdf2` (no new package): 210,000
iterations, 16-byte random salt, 32-byte derived hash, stored in a self-describing
format `PBKDF2$210000$<saltBase64>$<hashBase64>`. Verification tries the new format
first, falls back to the legacy unsalted-SHA256 format (what the seeder and login use
today), and transparently re-hashes to PBKDF2 on successful verification (password
upgrade on next login/reset).

**Rationale**: FR-006 requires salted one-way storage — current unsalted SHA256 does not
meet it. Seeded users (alice/bob/carol/admin, which `testUsers.ts` depends on) persist in
the dev database (`Database.Migrate()`, no drop), so the legacy format must keep
verifying. 210k SHA256 iterations is the prior OWASP recommendation and costs ~100–200 ms
on dev hardware, inside the <500 ms login goal. The self-describing format makes the
upgrade path safe and auditable (Constitution IV).

**Alternatives considered**:
- ASP.NET Core Identity `PasswordHasher<T>` — rejected: drags Identity's model into a
  non-Identity app; `KeyDerivation` is one sentence simpler (Constitution II).
- BCrypt — rejected: requires a NuGet package; Constitution tech constraints avoid new
  packages without a specific problem.
- Argon2id — rejected: no built-in .NET API, would require a native package.

## R3. Session invalidation on password reset

**Decision**: Add a `SecurityStamp` (Guid) column to `Student`. The sign-in cookie
carries a `SecurityStamp` claim. Cookie authentication's `OnValidatePrincipal` event
re-checks the stamp against the database (short in-process cache, ~60 s TTL) and signs
the user out on mismatch or a missing claim. A password reset rotates the stamp.

**Rationale**: FR-017 requires all pre-existing sessions to die after a reset. ASP.NET
Core cookie auth is stateless — there is no server-side session to revoke, so the only
general mechanism is validating a credential-generation marker at request time.
`OnValidatePrincipal` is the framework-standard hook; the in-process cache bounds the
cost to at most one lookup per user per minute (fine at dev scale; a cache miss only
costs a DB query — no Valkey involvement, Constitution VI).

**Alternatives considered**:
- Server-side session-token table — rejected: new table, per-request join, and cleanup
  logic for the same guarantee the stamp check already gives.
- No invalidation — rejected: violates FR-017.
- Cookie absolute-expiry tricks — rejected: the server cannot target one user's cookie
  from the server side.

## R4. Verification / reset tokens

**Decision**: Each link carries a 32-byte random token (base64url). Only the SHA-256 hex
of the token is stored, on `Student`: `VerificationTokenHash` +
`VerificationTokenExpiresAt` and `ResetTokenHash` + `ResetTokenExpiresAt` (all
nullable). Tokens are single-use (columns cleared on use); issuing a new token overwrites
any pending one; expiry is 24 h (verification) / 30 min (reset) per the spec. Lookup is
by hashed token + not-expired, then the row is consumed.

**Rationale**: Storing only the hash means a database leak does not leak working links
(parallel to R2's credential-storage posture). The spec's Key Entities allow at most one
pending token per purpose per account, so four nullable columns are sufficient — a token
table would be an extra layer a human has to read through (Constitution II). Tokens must
survive restarts until used, so they belong in MSSQL (Constitution VI: "would losing
this on a flush be fine?" — no, hence SQL, not cache).

**Alternatives considered**:
- Separate token table — rejected: supports at most one pending token per purpose;
  columns are simpler.
- Store raw tokens — rejected: leaks working links on a DB leak.
- Valkey for tokens — rejected: Constitution VI reserves Valkey for SCORM runtime state.

## R5. Email delivery seam + mock outbox

**Decision**: `ITransactionalEmailSender { Task SendAsync(OutboundEmail email) }` in
SharedKernel, with
`OutboundEmail(To, Purpose, Subject, Body)` and `Purpose ∈ {Verification, Welcome,
PasswordReset}`. Host registers `MockEmailSender`, which appends to an in-memory
`DevEmailOutbox` (bounded ring, ~200 newest-first entries) **and** logs the full message.
A developer-only viewer page (`/Dev/Outbox`) and a JSON API (`GET /api/dev/outbox`), both
gated to the Development environment, expose the recorded emails so humans and Playwright
can retrieve links. The mock sends zero real outbound email.

**Rationale**: FR-019..FR-022. The interface is the one-sentence seam (Constitution II):
"the place any module sends transactional email without knowing the provider." A future
SendGrid implementation is a new class + a DI registration change (recorded as ADR-0004).
The outbox is a dev artifact: losing it on restart is fine (Constitution VI's test), so
it is in-memory, not MSSQL. The JSON endpoint gives Principle XIII E2E tests a clean,
deterministic way to extract verification/reset links without scraping logs.

**Alternatives considered**:
- MSSQL outbox table — rejected: no durability requirement; adds migration surface for a
  dev artifact.
- Console logging only — rejected: links become awkward to use in E2E tests and manual
  verification.
- Local SMTP sink container — rejected: new docker-compose service for no behavioral
  gain.

## R6. Rate limiting (FR-010, FR-013, FR-018)

**Decision**: Application-level per-email throttling in a small `EmailThrottle`
(Enrollment/Application): thread-safe in-memory sliding windows with caps — sign-up
≤10 attempts per email per 24 h, reset requests ≤5 per email per hour, verification
resends ≤3 per email per hour. Throttled attempts return a friendly "please try again
later" outcome. Expired entries are purged opportunistically.

**Rationale**: The throttle key is the email address, which lives in the form body — the
built-in `AddRateLimiter` middleware would need a partitioner that reads the form
asynchronously, which is awkward. A service-level throttle sits next to the logic it
protects, is trivially unit-testable, and is proportionate to dev scale. In-memory state
loss on restart is acceptable (it is a dev safeguard, not a compliance control).

**Alternatives considered**:
- ASP.NET Core `AddRateLimiter` + `PartitionedRateLimiter` — rejected for the
  form-body keying reason; an IP-level backstop can be added later if ever needed.
- Valkey-backed counters — rejected: Constitution VI.

## R7. Common-password blocklist (FR-003)

**Decision**: Embed a curated top-1000 common-password list as a plain text resource
(`src/Modules/Enrollment/Resources/common-passwords.txt`, one lowercase entry per line,
~10 KB), loaded once into a `HashSet<string>`. A password equal to a list entry
(case-insensitive) is rejected. The list is a trimmed slice of the standard public
"most common passwords" lists.

**Rationale**: "Strict" (spec) needs more than character classes; a blocklist is the
industry-standard complement. 1000 entries is small, human-auditable (Constitution IV),
and has no runtime dependency.

**Alternatives considered**:
- NuGet package with a 1M+ word list — rejected: package + memory bloat for a learning
  project.
- No blocklist — rejected: FR-003 explicitly includes it.

## R8. Email normalization

**Decision**: Normalize every email at the boundary — `Trim().ToLowerInvariant()` — in
sign-up, sign-in, verification resend, reset request, and user provisioning; store the
normalized value. The existing unique index on `Students.Email` runs under MSSQL's
default case-insensitive collation, so database-level uniqueness and app-level comparison
agree.

**Rationale**: FR-002/FR-026. Current code compares `s.Email == email` directly; SQL-level
CI collation already makes the DB comparison case-insensitive, but explicit
normalization removes any ambiguity for in-memory checks (e.g., the throttle and
duplicate pre-checks) and guarantees stored values are canonical.

**Alternatives considered**:
- Database computed column / alternate-collation index — rejected: overkill; the default
  collation already gives CI uniqueness.

## R9. Baseline gate remediation (pre-existing boundary violations)

**Decision**: 027 carries a prerequisite, **behavior-preserving** workstream that makes
`ModuleBoundaryTests` green:

- New `Enrollment.Contracts`:
  - `IUserProvisioning` — create (name, email, password, role, orgId, verified), get by
    id, list by org scope/role, update (name/role/org), delete. (Covers `UserService`.)
  - `IUserLookup` — user scope info (role, org) by id; learner counts per org; totals.
    (Covers `UserInfoLookup`, `OrganizationService`, `DashboardService` counts.)
  - `IEnrollmentAdmin` — enroll/unenroll a student in a course (with existence checks),
    list a student's enrollments, totals, recent enrollments with learner info.
    (Covers `AdminEnrollmentService`, `DashboardService` recents.)
- Extended `Catalog.Contracts`:
  - `ICourseLookup` gains `CountAsync()`, `CountByOrgAsync(orgId)`,
    `GetCoursesAsync(IEnumerable<Guid> ids)`.
  - New `ICourseAdmin.DeleteAsync(courseId)` (`CourseVisibilityService` deletes courses).
- The six violating Management services + `ManagementSeeder` are refactored to depend
  only on Contracts.
- Seeder reorg: `ManagementSeeder` seeds organizations only; the seeded SuperUser
  `Student` row moves to `EnrollmentSeeder` (which already hard-codes the root org id).
  Host startup calls ManagementSeeder → EnrollmentSeeder (order preserved).

**Rationale**: Constitution III + Development Workflow ("ArchitectureTests must pass
before a slice is considered done"). 027's new code is boundary-clean by design (R1), but
the slice's completion gate includes the 10 legacy violating types, so they must be
fixed for 027 to be "done". The work is mechanical (introduce contract, delegate,
re-register DI) and behavior-preserving, so it is folded in as an ordered task group
rather than a separate spec cycle. It is self-contained and could be split into its own
bug spec if the user prefers — flagged in the completion report.

**Alternatives considered**:
- Separate bug spec first — valid, but adds a full specify→plan→tasks cycle for a
  prerequisite the gate requires; the remediation is scoped tightly and ordered before
  feature tasks.
- Weaken/skip the boundary tests — rejected: the Constitution makes the failing build
  the boundary ("A convention that relies on memory or code review isn't a boundary; a
  failing build is.").

## R10. Verification state for pre-existing accounts

**Decision**: New column `IsEmailVerified` (bool) with a **database default of true**.
Self-service sign-up explicitly sets `false`; admin provisioning and the seeder explicitly
set `true`.

**Rationale**: Spec Assumption 2 (existing/seeded accounts are treated as verified). The
dev database persists across runs (`Migrate()`, no drop), so the migration's default value
decides whether existing users — including Playwright's seeded logins — stay signed-in
able. Default-true avoids locking out every pre-existing user; new signups opt in to
`false` explicitly, which is the only unverified path.

**Alternatives considered**:
- Per-row data migration setting `true` — equivalent outcome, more moving parts.
- Default `false` + a follow-up migration — rejected: lockout risk for existing users.
