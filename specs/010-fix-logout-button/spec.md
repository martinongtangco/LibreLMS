# Spec 010: Fix Logout Button

**Status**: Complete (merged 2026-07-31)

## Problem

The Logout button in the navigation bar does not work. Clicking it results in a 404 error.

## Root Cause

The `Logout.cshtml` Razor page template is missing. Only `Logout.cshtml.cs` (the code-behind) exists. In ASP.NET Core Razor Pages, the `.cshtml` file with the `@page` directive is required to register the route — without it, the `/Account/Logout` endpoint is not recognized.

Additionally, `Logout.cshtml.cs` has two bugs:
1. `OnGet()` calls `SignOutAsync()` without awaiting it (fire-and-forget), risking a redirect before sign-out completes
2. Both `OnGet()` and `OnPostAsync()` use `Response.Redirect()` which throws `ThreadAbortException`. Razor Pages should return `IActionResult`.

## Proposed Fix

1. Create `Logout.cshtml` with `@page` directive
2. Fix `Logout.cshtml.cs`:
   - Make `OnGet()` return `IActionResult` and properly await `SignOutAsync()`
   - Replace `Response.Redirect()` with `Redirect()` return values in both handlers

## Files Changed

- `src/Host/Pages/Account/Logout.cshtml` (new)
- `src/Host/Pages/Account/Logout.cshtml.cs` (modified)
