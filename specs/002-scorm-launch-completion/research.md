# Research: SCORM Launch & Completion

**Branch**: `002-scorm-launch-completion` | **Date**: 2025-07-29

## Decision 1: SCORM 1.2 Manifest Parsing

**Decision**: Parse `imsmanifest.xml` using `System.Xml.Linq` (LINQ to XML) to extract the launch SCO and content file references.

**Rationale**: LINQ to XML is built into .NET with no additional dependencies. SCORM 1.2 manifests use the ADL XSI namespace (`http://www.imsglobal.org/xsd/imsmanifest_v1p2`) and are typically small (single SCO). A full XML parser like `System.Xml.XmlDocument` is overkill; `XDocument` provides clean querying with minimal code.

**Alternatives considered**:
- `System.Text.Xml` (newer, streaming) — not needed for small manifest files
- Third-party SCORM libraries — unnecessary complexity for a simplified subset; we only need manifest parsing, not full SCORM runtime compliance

## Decision 2: SCORM API Shim Pattern

**Decision**: Serve the SCORM API as a JavaScript endpoint (`/api/scorm/api.js`) that the SCORM content's `html` includes via a `<script>` tag. The JS calls back to server endpoints for state operations.

**Rationale**: SCORM 1.2 content expects `window.API` (or `parent.API`) with methods `LMSInitialize`, `LMSFinish`, `LMSGetValue`, `LMSSetValue`, `LMSCommit`. The standard approach is to inject this script into the SCORM content's HTML wrapper page. For our architecture, the wrapper page is a Razor Page that serves the SCORM content's HTML and injects the API script. The JS layer communicates with server endpoints via `fetch()` calls.

**Alternatives considered**:
- Full in-browser JS-only API (all state in localStorage) — doesn't support cross-tab prevention or server-side commit
- SignalR real-time API — overkill for SCORM's synchronous API pattern
- Server-sent events for state sync — unnecessary complexity for single-user sessions

## Decision 3: SCORM Content Serving Strategy

**Decision**: Extract uploaded ZIP packages to a per-package directory under `wwwroot/scorm-content/{packageId}/`. Serve as static files via ASP.NET Core's static file middleware. The SCORM wrapper page sets `baseUrl` to the package's content directory.

**Rationale**: ASP.NET Core's static file middleware is optimized and handles caching, MIME types, and range requests. Extracting to disk avoids ZIP-streaming overhead and allows the content to reference relative URLs naturally. The `wwwroot` path integrates cleanly with the existing Host project structure.

**Alternatives considered**:
- Stream files from ZIP on each request — slower, no caching, complex path resolution
- Store in Valkey as blobs — Valkey is for session state, not content storage
- Use a separate CDN — overkill for a learning project with small packages

## Decision 4: Session State with StackExchange.Redis

**Decision**: Use `StackExchange.Redis` (already referenced via the project's Valkey support) to store the `cmi.*` bag as a JSON hash per session key. Key pattern: `scorm:session:{sessionId}`. On `LMSCommit`/`LMSFinish`, read the hash from Valkey and write the durable record to MSSQL via EF Core.

**Rationale**: StackExchange.Redis works identically against Valkey (Redis-protocol-compatible). The hash data type maps naturally to the `cmi.*` key/value model. TTL can be set on the key for automatic session expiration (defaulting to 30 minutes, adjustable).

**Alternatives considered**:
- In-process dictionary — lost on restart, doesn't support the polyglot storage principle
- MSSQL only for session state — violates the polyglot storage rationale (ADR-0003); too many writes for in-progress state
- Azure Cache for Redis — not available in the devcontainer environment

## Decision 5: Session Timeout

**Decision**: 30-minute inactivity timeout. Valkey key TTL set to 30 minutes. Client-side `beforeunload` handler calls `LMSCommit()` on tab close. If the session expires in Valkey (server restart, network issue), the student resumes from the last committed MSSQL checkpoint.

**Rationale**: 30 minutes is the SCORM 1.2 default session time recommendation. It's long enough for typical course interactions but prevents orphaned sessions from consuming Valkey memory indefinitely. The `beforeunload` handler (clarification Q1) ensures most sessions commit cleanly.

**Alternatives considered**:
- No timeout — orphaned sessions consume memory indefinitely
- 15 minutes — too short for longer SCORM courses
- 60+ minutes — unnecessary memory consumption for a learning project

## Decision 6: CourseAttempt vs Enrollment Relationship

**Decision**: `CourseAttempt` is a separate entity from `Enrollment`. An enrollment represents the student's right to take a course; an attempt represents a single pass through the SCORM content. A student can have multiple attempts per enrollment.

**Rationale**: The existing `Enrollment` entity (in the Enrollment module) tracks that a student is registered for a course. `CourseAttempt` tracks each time the student launches and progresses through the SCORM content. This separation allows retakes (multiple attempts per enrollment) and preserves the history of each attempt's score and completion status.

**Relationship**: `Enrollment` (1) → (0..*) `CourseAttempt` → (1) `Course`. The Scorm module references `Enrollment` data through the Enrollment module's contracts (to be defined) or queries its own `CourseAttempt` table which stores `StudentId` and `CourseId` as foreign keys.

**Alternatives considered**:
- Add attempt data to `Enrollment` — violates separation of concerns; Enrollment module shouldn't know about SCORM
- Add attempt count to `Enrollment` — loses per-attempt detail (score, suspend data)

## Decision 7: Package Upload Validation

**Decision**: Validate uploaded ZIP files by checking for `imsmanifest.xml` at the root of the archive and parsing it to extract at least one `item` with `isvparameters` or a valid `identifier` referencing an `asset`. Reject files that fail validation with a descriptive error.

**Rationale**: Basic validation prevents broken packages from appearing in the catalog (FR-012). Full SCORM 1.2 compliance checking is unnecessary; we only need to ensure the manifest exists and has a launchable SCO.

**Alternatives considered**:
- No validation — broken packages would cause confusing errors at launch time
- Full SCORM conformance checker — overkill for a learning project; would require a significant third-party library

## Decision 8: Scorm Module Dependencies

**Decision**: The Scorm module depends on:
- `SharedKernel` — `Entity<TId>`, `Result<T>`
- `Catalog.Contracts` — `ICourseLookup`, `CourseSummary` (to validate course exists)
- `Enrollment.Contracts` (new) — `IEnrollmentLookup` (to validate student is enrolled)
- `StackExchange.Redis` — for Valkey session storage
- `System.Formats.Asn1` or standard ZIP handling — `System.IO.Compression` for ZIP extraction

**Rationale**: The module boundary principle (Constitution III) requires the Scorm module to access other modules' data through their `.Contracts` projects. It needs to validate that (a) the course exists (Catalog.Contracts) and (b) the student is enrolled (Enrollment.Contracts, new interface). StackExchange.Redis is the Valkey client. System.IO.Compression is built into .NET for ZIP handling.

**Note**: This requires adding an `IEnrollmentLookup` interface to `Enrollment.Contracts` — a small new contract that the Enrollment module implements to answer "is student X enrolled in course Y?"

**Alternatives considered**:
- Direct database access from Scorm module — violates module boundaries (Constitution III)
- Duplicate enrollment check in Scorm module's own table — creates data inconsistency risk
- Use Enrollment module's API endpoints — adds HTTP call complexity within the monolith; contracts are the preferred pattern
