# Bug 001: RBAC Layout Fix — Learner-Admin Toggle and Missing Admin Menu

## Problem

1. **Regular Learners see the Learner-Admin toggle** — The role-pill toggle is rendered for ALL authenticated users in `_Layout.cshtml` with no server-side role check. This is misleading and potentially dangerous.
2. **Admin links are hidden for Administrators** — CSS defaults admin links to `display: none`, and JS defaults localStorage to `'learner'`. The server never communicates the user's actual role to the client.
3. **`Admin/Courses/Create.cshtml.cs` missing `[Authorize]`** — The page model has no authorization attribute, allowing unauthenticated access.

## Root Cause

The role-pill and nav-link visibility are purely client-side (localStorage), with no server-side role communication in the Razor layout. The layout treats all authenticated users identically regardless of their actual claims.

## Fix

### `_Layout.cshtml`
- Only render the role-pill toggle for users with `SuperUser` or `OrgAdmin` roles
- Pass the user's actual role to the layout via `User.IsInRole()` checks
- Set the correct default role view in JS based on server-side role
- For pure Learners: hide toggle entirely, no admin links
- For SuperUser/OrgAdmin: show toggle, default to "admin" view

### `Admin/Courses/Create.cshtml.cs`
- Add `[Authorize(Roles = "SuperUser,OrgAdmin")]` attribute

## Constitution Principles Applied

- VIII (Branching Discipline): Working on `bug/001-rbac-layout-fix`
- X (No Ad-Hoc Fixes): Documented root cause before coding
- III (Module Boundaries): No cross-module changes — layout and page models only
