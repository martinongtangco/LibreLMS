# Data Model: Clickable Brand Link to Home

**Feature**: 029-clickable-home-brand
**Date**: 2026-08-16

## Summary

This feature introduces no data model changes. It is a purely presentational
navigation change (navbar brand becomes a link to Home).

## Client-Side State (No Changes)

### `nav-role-view` (localStorage)

- **Type**: String (`"learner"` | `"admin"`)
- **Purpose**: Persist the user's preferred role view across navigations
- **Impact**: None — the brand link targets Home regardless of this value
  (FR-006). No new localStorage keys are introduced.

## Server-Side State (No Changes)

No new database tables, entities, or application state. The change references:
- The existing root route (`GET /` → 302 → `/Courses`) in `Program.cs` —
  unchanged.
- `User.Identity` in `_Layout.cshtml` — unchanged; the brand renders
  identically in both auth branches.

## Existing Entities (Unchanged)

- **Course catalog / enrollment / user data**: untouched.
- **Navigation markup**: `_Layout.cshtml` — `.brand` element changes type from
  `span` to `a` (no data involved).
