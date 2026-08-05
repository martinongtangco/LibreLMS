# Data Model: Navigation Bar and Account Menu

**Feature**: 020-nav-account-menu
**Date**: 2025-01-31

## Summary

This feature introduces no data model changes. All navigation state is managed client-side.

## Client-Side State (localStorage)

### `nav-role-view`

- **Type**: String (`"learner"` | `"admin"`)
- **Purpose**: Persist the user's preferred view role across page navigations
- **Default**: `"learner"`
- **Set by**: Clicking Learner/Admin toggle buttons in the role pill
- **Read by**: Nav initialization script on every page load
- **Note**: Carried over from 018-nav-design-alignment; no change to this key

## Server-Side State (No Changes)

No new database tables, entities, or application state. The navigation uses:
- `User.Identity.Name` — existing authenticated user principal
- `Context.Request.Path` — existing Razor Pages request context for active link detection
- Existing role claims — referenced only for initial state, not for toggling

## Existing Entities (Unchanged)

- **Settings**: Email notifications toggle, theme preference — already in database
- **User**: Name, roles — already in database
- **Logout**: Session termination — existing endpoint at `/Account/Logout`
