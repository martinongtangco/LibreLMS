# ADR-0005: No HTMX for Page Navigation

**Status**: Accepted
**Date**: 2025-01-XX
**Supersedes**: N/A

## Context

During UI redesign work (specs 017/018), HTMX attributes (`hx-get`, `hx-target`, `hx-push-url`) were added to navigation links (e.g., "View Details" buttons on course cards) to enable SPA-like in-place page swaps. This required:

1. A backend `OnGetDetailAsync` handler returning a `PartialViewResult`
2. A `_CourseDetail.cshtml` partial view
3. A `#main-content` wrapper div on the target page

The handler and partial were never implemented. The buttons silently failed — clicks produced no visible action and no console errors (HTMX swallows failed responses by default). This pattern repeated: every UI change that touched these links risked breaking them with zero compile-time feedback.

**Root cause**: HTMX handler references (`hx-get="?handler=Detail"`) are plain HTML strings. There is no compile-time verification that the referenced handler method exists on the PageModel. Contrast with Razor's `asp-page-handler="Detail"`, which is validated by the tag helper system at build time.

## Decision

### Navigation links use Razor tag helpers only

All `<a>` tags that navigate between pages MUST use `asp-page` and `asp-route-*` attributes exclusively. No HTMX attributes on navigation links.

```html
<!-- CORRECT — compile-time verified, works without any backend handler -->
<a asp-page="/Courses/Detail" asp-route-id="@Model.Id" class="btn">View Details</a>

<!-- WRONG — string reference, silently breaks if handler doesn't exist -->
<a asp-page="/Courses/Detail" asp-route-id="@Model.Id"
   hx-get="/Courses/Detail?handler=Detail"
   hx-target="#main-content"
   hx-push-url="true"
   class="btn">View Details</a>
```

### HTMX is permitted for form submissions and partial-content AJAX

HTMX remains valid and encouraged for:

- Form submissions that swap a result region (`hx-post` + `hx-target` + `hx-swap="outerHTML"`)
- Search/filter inputs that reload a list partial (`hx-get` on `<input>`/`<select>`)
- Explicit refresh buttons that reload a content region

These patterns are lower risk because the handler is created alongside the feature (not as an afterthought), and the target region is on the same page.

### Every HTMX handler is registered in architecture tests

`ArchitectureTests/HtmxHandlerTests.cs` maintains a registry of all HTMX handler references. Two tests enforce:

1. **Handler existence**: Every registered handler maps to a real `OnGetXAsync` or `OnPostXAsync` method on its PageModel.
2. **No orphaned references**: Every `handler=XYZ` string found in .cshtml files is registered in the test.

Adding a new HTMX handler requires updating the test registry. The build fails if a handler reference exists without a corresponding method.

## Consequences

**Positive**:
- Navigation buttons can no longer silently break — Razor tag helpers are compile-time verified
- Missing HTMX handlers are caught by the test suite, not by users clicking dead buttons
- Simpler .cshtml files — no dual `asp-page` + `hx-get` attributes doing the same thing

**Negative**:
- Loss of SPA-like in-place page transitions for navigation (the browser does a full page load)
- New HTMX endpoints require a test registry entry (minor overhead)
- If SPA-like transitions are desired later, they require a proper implementation (partial view + handler + test) — not just slapping `hx-get` on a link

## Related

- Spec 017 (Organic UI Redesign) — introduced the broken HTMX navigation pattern
- Spec 018 (Nav Design Alignment) — carried the pattern forward
- Bug 019 — fix for broken View Details button and other button failures
