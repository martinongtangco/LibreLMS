# Spec 011: Fix Logout Authentication Scheme Name

**Status**: Complete (merged 2026-07-31)

## Problem

After the logout button fix (spec 010), clicking Logout throws:
```
InvalidOperationException: No sign-out authentication handler is registered for the scheme 'Cookies'.
The registered sign-out schemes are: Cookie.
```

## Root Cause

`Program.cs` registers the cookie scheme as `"Cookie"` (singular):
```csharp
.AddCookie("Cookie", options => { ... })
```

But `Logout.cshtml.cs` uses `CookieAuthenticationDefaults.AuthenticationScheme` which resolves to `"Cookies"` (plural). The names don't match.

## Proposed Fix

Replace `CookieAuthenticationDefaults.AuthenticationScheme` with `"Cookie"` in `Logout.cshtml.cs` to match the registered scheme name.

## Files Changed

- `src/Host/Pages/Account/Logout.cshtml.cs` (modified)
