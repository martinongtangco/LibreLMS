# Feature Specification: Rebrand to Libre LMS

**Feature Branch**: `story/008-rebrand-libre-lms`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/008-rebrand-libre-lms`.

**Created**: 2025-07-30

**Status**: Complete (merged 2026-07-31)

**Input**: User description: "I want to change the branding of this application and name it as Libre LMS"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See "Libre LMS" branding throughout the application (Priority: P1)

A user visits any page of the application and sees the name "Libre LMS" in the page title, headers, navigation, and any other place the application name appears. The old name "Learning LMS" should not be visible to users anywhere in the UI.

**Why this priority**: This is the core user-facing requirement. If the branding is not consistently changed, the rebrand is incomplete and users will be confused.

**Independent Test**: Can be fully tested by navigating to every user-visible page (home, login, course catalog, course detail, SCORM launch, admin pages) and verifying "Libre LMS" appears and "Learning LMS" does not.

**Acceptance Scenarios**:

1. **Given** a user is on any page of the application, **When** they look at the page title (browser tab), **Then** it displays "Libre LMS" or a title beginning with "Libre LMS"
2. **Given** a user is on any page, **When** they look at the header or navigation bar, **Then** the application name displayed is "Libre LMS"
3. **Given** a user is on any page, **When** they search the visible text, **Then** they do not find any instance of "Learning LMS" or "LearningLms" as application-facing text

---

### User Story 2 - Documentation reflects the new name (Priority: P2)

A developer or administrator reads the project documentation (README, constitution, ADRs) and sees the application referred to as "Libre LMS" consistently.

**Why this priority**: Documentation is the primary reference for new contributors and stakeholders. Inconsistent naming creates confusion about whether the rebrand was completed.

**Independent Test**: Can be fully tested by searching the documentation files for "Learning LMS" / "LearningLms" references and confirming they have been updated to "Libre LMS" / "LibreLms".

**Acceptance Scenarios**:

1. **Given** a reader opens the README.md, **When** they search for the application name, **Then** they see "Libre LMS" as the primary name used
2. **Given** a reader opens the constitution document, **When** they read it, **Then** the application is referred to as "Libre LMS"
3. **Given** a reader opens any documentation file, **When** they search for "Learning LMS", **Then** they find no instances (or only historical references in ADR changelogs with clear context)

---

### User Story 3 - Internal naming is updated consistently (Priority: P3)

A developer working on the codebase sees the new name reflected in namespaces, project names, database names, solution files, and other internal identifiers.

**Why this priority**: Internal consistency matters for maintainability but does not directly affect end users. This supports developer experience and prevents confusion when reading code.

**Independent Test**: Can be fully tested by searching the entire codebase for "LearningLms" / "Learning LMS" patterns and confirming they are updated.

**Acceptance Scenarios**:

1. **Given** a developer searches the codebase for "LearningLms", **When** they review the results, **Then** all namespace declarations, project names, and identifiers use "LibreLms" instead
2. **Given** a developer checks the solution file, **When** they open it, **Then** it is named to reflect "LibreLms"
3. **Given** a developer checks the database connection configuration, **When** they review it, **Then** the database name reflects "LibreLms"

---

### Edge Cases

- What happens when existing database migrations reference "LearningLms" in their namespace? — Migration files retain their original namespace for compatibility; only the active namespace references are updated.
- How are existing git history and past spec slices handled? — Historical files in completed spec slices (001–007) and git history are left unchanged; the rebrand applies forward from this slice.
- What about the `appsettings.Development.json` database name? — The database name should be updated to match, and a note should be made for any environment that needs to recreate the database.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All user-facing text displaying the application name must show "Libre LMS" instead of "Learning LMS"
- **FR-002**: All web pages, layouts, and templates must reference "Libre LMS" in titles, headers, and navigation
- **FR-003**: All documentation files (README, constitution, ADRs) must reference "Libre LMS" as the application name
- **FR-004**: All source code identifiers, namespaces, and module names must use "LibreLms" instead of "LearningLms"
- **FR-005**: The project solution file must be renamed from "LearningLms" to "LibreLms"
- **FR-006**: Database name in configuration must be updated from "LearningLms" to "LibreLms"
- **FR-007**: Deployment and container configuration files must reflect "LibreLms" if they currently reference "LearningLms"
- **FR-008**: Existing git history, completed spec directories (001–007), and migration snapshot files may retain "LearningLms" references for backward compatibility — these are explicitly excluded from renaming

### Key Entities

- **Application Branding**: The visible name presented to users across all touchpoints (UI, documentation, configuration). This is the primary entity being changed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A full-text search of all user-facing pages returns zero instances of "Learning LMS" or "LearningLms"
- **SC-002**: The README and constitution documents use "Libre LMS" as the primary application name with no unqualified references to "Learning LMS"
- **SC-003**: The application remains fully functional after the rebrand — all existing features work identically
- **SC-004**: A search of active source and configuration files for "LearningLms" or "Learning LMS" returns zero results (excluding migration snapshots and historical spec files)

## Assumptions

- The rebrand is cosmetic and organizational — no functional behavior of the application changes
- The application is not yet deployed to production; there are no live users affected by the name change
- Database can be recreated with the new name if needed (no production data loss concern)
- The project is still in early development; completed spec slices (001–007) are historical records and do not need renaming
- All namespace, project, and file renames are handled consistently using the development toolchain's refactoring capabilities
