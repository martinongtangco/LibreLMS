# Spec 024: Fix Enroll Button — Missing Antiforgery Import

**Status**: Complete (merged 2026-08-10)

## Problem
The "Enroll now" button on the course detail page returns HTTP 400 (Bad Request) and does nothing. The HTMX POST to `OnPostEnrollAsync` is rejected by Razor Pages' antiforgery validation.

## Root Cause
`Detail.cshtml.cs` uses `[IgnoreAntiforgeryToken]` on `OnPostEnrollAsync()` to exempt HTMX POSTs from antiforgery validation. However, the file is missing `using Microsoft.AspNetCore.Antiforgery;`. Without this import, the attribute resolves through implicit usings but does not apply the exemption — the antiforgery filter still validates the token, and since HTMX doesn't send one, the request is rejected with 400.

`Logout.cshtml.cs` has the same attribute but includes the explicit `using Microsoft.AspNetCore.Antiforgery;` and works correctly.

## Fix
Add `using Microsoft.AspNetCore.Antiforgery;` to `Detail.cshtml.cs`.

## Files Changed
- `src/Host/Pages/Courses/Detail.cshtml.cs` — add missing using directive

## Verification
- Playwright enrollment test passes
- Manual test: log in → navigate to course detail → click "Enroll now" → enroll region updates
