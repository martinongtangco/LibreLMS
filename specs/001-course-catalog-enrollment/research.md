# Research & Clarifications: Course Catalog & Enrollment

**Date**: 2025-07-29
**Feature**: [spec.md](./spec.md)

## Decisions Resolved

### 1. Anonymous Catalog Access

**Decision**: Catalog browsing is public; authentication required for enrollment and "My Enrolled Courses".

**Rationale**: Standard LMS pattern. Reduces friction for discovery while gating actions that require identity. FR-012 already requires auth for enrollment and enrolled courses; extending that to mean "catalog is open, actions require auth" is the most natural reading.

**Alternatives considered**:
- Auth required for all pages — increases friction, no security benefit for read-only course listings
- Auth required only for enrollment but not enrolled courses — exposes personal data, worse than the chosen option

### 2. Un-enrollment (Dropping a Course)

**Decision**: Out of scope for this slice. Enrollment is permanent until future slices add management.

**Rationale**: The spec describes enrollment as a one-way action. Adding un-enrollment introduces state transitions, UI for confirmation, and potential edge cases (can you un-enroll mid-attempt?) that are better addressed after the core flow is solid.

**Alternatives considered**:
- Include un-enrollment — adds ~20% more scope for marginal value in slice 1
- Include "pause/resume" enrollment — overly complex for a learning exercise

### 3. Student Identity Model

**Decision**: Use a simple `UserId` (guid) as the student identifier. Authentication mechanism is deferred — seed data will create students with known IDs for demonstration.

**Rationale**: The constitution states "Students are authenticated users with persistent identities." The spec doesn't require building auth infrastructure — it requires the system to work with authenticated students. Seeded test users satisfy both the spec and the learning goals without building an auth system that isn't part of this slice.

**Alternatives considered**:
- Build full auth (email/password) — not in scope, would dominate slice 1
- Use external auth (OAuth2) — adds dependency, not needed for learning goals

### 4. Catalog Filter/Search Implementation

**Decision**: Simple text filter on course name + category dropdown. No full-text search or fuzzy matching.

**Rationale**: FR-002 requires filter/search capability. For a learning project with a small number of seeded courses, a text filter and category selector are sufficient and demonstrate the pattern without over-engineering.

**Alternatives considered**:
- Full-text search (Elasticsearch) — disproportionate for the data volume
- Only category filter — insufficient per FR-002 which mentions course name

### 5. Web Portal Technology Choice

**Decision**: Razor Pages for the web portal.

**Rationale**: The constitution says "Razor Pages or Blazor Server, chosen at `/speckit.plan` time for whichever slice needs it first — default to whichever option needs the fewest moving parts." Razor Pages is the simpler choice: server-rendered HTML, no JavaScript framework, fewer concepts to learn, and aligns with the constitution's preference for explicit over clever.

**Alternatives considered**:
- Blazor Server — more interactive but requires SignalR connection, more concepts, marginal benefit for this slice's read-heavy pages
- SPA (React/Vue) — adds build pipeline, state management, unnecessary for server-rendered catalog
