# Feature Specification: [FEATURE NAME]

**Feature Branch**: `[###-feature-name]`

**Created**: [DATE]

**Status**: Draft

**Input**: User description: "$ARGUMENTS"

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.

  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - [Brief Title] (Priority: P1)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently - e.g., "Can be fully tested by [specific action] and delivers [specific value]"]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]
2. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 2 - [Brief Title] (Priority: P2)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 3 - [Brief Title] (Priority: P3)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

[Add more user stories as needed, each with an assigned priority]

### Edge Cases

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right edge cases.
-->

- What happens when [boundary condition]?
- How does system handle [error scenario]?

## Acceptance Examples *(mandatory)*

<!--
  One row per independently testable acceptance scenario, written so a test author
  who has NEVER seen the implementation can compute the expected value from this
  table alone. Inputs must be fully concrete (exact values, exact state).
  Every expected value carries a confirmation mark:
    [CONFIRMED]   — the user verified this value against the spec
    [NEEDS-USER]  — the user must confirm/correct this value before the oracle
                    stage runs (a spec with [NEEDS-USER] rows is blocked at the
                    clarify checkpoint)
  Expected values that depend on implementation choices (constants, formats)
  must have those choices stated in this section or in Invariants, not left
  to the reader.
-->

| # | Input (fully concrete) | Expected output (exact value / status / message) | Confirmed |
|---|------------------------|--------------------------------------------------|-----------|
| 1 | [concrete input]       | [exact expected output]                          | [NEEDS-USER] |
| 2 | [concrete input]       | [exact expected output]                          | [NEEDS-USER] |

## Invariants *(mandatory)*

<!--
  Properties that must hold for ALL inputs, not just the examples above.
  Each invariant: a stable name, a one-sentence property, and the test
  strategy (property test via FsCheck / exhaustive fixture / boundary fixture).
  An invariant is the contract a red team attacks first — if the spec cannot
  state it, the spec is not ready.
-->

1. **INV-1 [name]**: [one-sentence property]. *Test strategy: [FsCheck property / fixture / boundary]*.
2. **INV-2 [name]**: [one-sentence property]. *Test strategy: [...]*. 

## Abuse & Edge Cases *(mandatory)*

<!--
  Extends Edge Cases: what happens when the input is hostile, degenerate, or
  beyond the happy path. Each row names the required behavior INCLUDING the
  failure mode ("must return 400, not 500" / "must terminate in ≤ N steps",
  "must not hang" / "must reject, message: ...").
  A case with no defined failure mode is a gap the red team will find for you.
-->

| Case (malformed / cyclic / oversized / concurrent / empty / boundary) | Required behavior (incl. failure mode) |
|----------------------------------------------------------------------|----------------------------------------|
| [case]                                                               | [behavior + failure mode]              |

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST [specific capability, e.g., "allow users to create accounts"]
- **FR-002**: System MUST [specific capability, e.g., "validate email addresses"]
- **FR-003**: Users MUST be able to [key interaction, e.g., "reset their password"]
- **FR-004**: System MUST [data requirement, e.g., "persist user preferences"]
- **FR-005**: System MUST [behavior, e.g., "log all security events"]

*Example of marking unclear requirements:*

- **FR-006**: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]
- **FR-007**: System MUST retain user data for [NEEDS CLARIFICATION: retention period not specified]

### Key Entities *(include if feature involves data)*

- **[Entity 1]**: [What it represents, key attributes without implementation]
- **[Entity 2]**: [What it represents, relationships to other entities]

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]

## Assumptions

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right assumptions based on reasonable defaults
  chosen when the feature description did not specify certain details.
-->

- [Assumption about target users, e.g., "Users have stable internet connectivity"]
- [Assumption about scope boundaries, e.g., "Mobile support is out of scope for v1"]
- [Assumption about data/environment, e.g., "Existing authentication system will be reused"]
- [Dependency on existing system/service, e.g., "Requires access to the existing user profile API"]
