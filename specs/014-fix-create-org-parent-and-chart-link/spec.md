# Bug Fix: Create Organization ignores ParentId + Org Chart not discoverable

**Branch**: `bug/013-fix-create-org-and-chart-discovery`

**Created**: 2025-08-01

**Status**: Complete (merged 2026-07-31)

## Bug 1 — Create Organization always fails with "A root organization already exists"

### Root Cause
In `src/Host/Pages/Admin/Organizations/Create.cshtml.cs`, the `OnPostAsync` method parses the selected parent ID into a throwaway variable:

```csharp
Guid? parentId = null;
if (!string.IsNullOrWhiteSpace(Input.ParentId))
    Guid.TryParse(Input.ParentId, out var parsedParentId);
```

`parsedParentId` is never assigned to `parentId`, so `parentId` is always `null`. This triggers the "single root" validation in `OrganizationService.CreateAsync()` on every submission.

### Fix
Assign the parsed result to `parentId`.

## Bug 2 — Org Chart not discoverable from Organization Management page

### Root Cause
The Organization Management Index page (`/Admin/Organizations/Index`) was not updated to surface the new interactive Org Chart view. The link exists only in the navbar, making it easy to miss.

### Fix
Add a prominent link/button on the Index page to navigate to the Org Chart view.
