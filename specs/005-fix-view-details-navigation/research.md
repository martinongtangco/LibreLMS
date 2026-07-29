# Research: Fix Course View Details Navigation

## Decision: Separate full-page navigation from HTMX inline swap

**Rationale**: The current `_CourseCard.cshtml` puts `asp-page`, `hx-get`, `hx-target`, and `hx-push-url` on the same `<a>` element. HTMX intercepts the click, fetches a partial view via `?handler=Detail`, swaps it into `#main-content`, and pushes the handler URL into the browser address bar. On refresh, the browser hits `OnGetDetailAsync` which returns `Partial("_CourseDetail")` — a partial view without the layout, producing a broken page.

**Alternatives considered**:
1. **Remove HTMX from the card entirely** — simplest fix, but loses the SPA-like inline navigation that spec 004 wants. Would require spec 004 to re-implement it cleanly.
2. **Fix `hx-push-url` to push the clean URL** — set `hx-push-url="/Courses/Detail?id={guid}"` (without `handler=Detail`). This keeps HTMX inline swap but fixes the bookmark/refresh issue. **CHOSEN APPROACH.**
3. **Make `OnGetDetailAsync` render a full page** — possible but wrong; partial handlers should return partials. The full page handler is `OnGetAsync`.

## Decision: Use `hx-push-url` with clean path, not handler path

**Rationale**: HTMX's `hx-push-url` accepts either `true` (pushes the request URL) or a specific path. Currently the request URL is `/Courses/Detail?id=X&handler=Detail` (from `hx-get`), so `hx-push-url="true"` pushes the handler URL. Setting `hx-push-url="/Courses/Detail?id={guid}"` pushes the clean URL instead. When the user refreshes, the clean URL hits `OnGetAsync` which renders the full page.

**Alternatives considered**:
1. **Don't use `hx-push-url` at all** — the URL stays as `/Courses` (catalog) even after showing detail inline. Confusing for bookmarks and browser history.
2. **Use `hx-boost` instead of `hx-get` + `hx-push-url`** — `hx-boost` handles URL pushing automatically but also boosts all links in the element's subtree, which could interfere with other links on the page.

## Decision: Keep `asp-page` tag helper as fallback

**Rationale**: The `asp-page="/Courses/Detail" asp-route-id="@Model.Id"` generates the `href="/Courses/Detail?guid={id}"` attribute. When HTMX is unavailable (JavaScript disabled, CDN blocked, network error), the browser follows `href` as a normal link — full page navigation. This is the graceful degradation required by FR-006 and SC-005.

## Decision: No changes to `OnGetDetailAsync` handler

**Rationale**: The `OnGetDetailAsync` handler returns `Partial("_CourseDetail", model)` which is correct for HTMX swaps. It should NOT render a full page. The fix is on the client side (push the clean URL) so that refresh hits `OnGetAsync` instead. The handler remains as the HTMX swap target.

## Decision: No changes to course data fetching or enrollment logic

**Rationale**: Both `OnGetAsync` and `OnGetDetailAsync` already fetch course data, enrollment status, and SCORM package info correctly. The bug is purely in the client-side navigation (HTMX URL management), not in the server-side logic.

## Code Issues Identified

| File | Issue | Fix |
|------|-------|-----|
| `_CourseCard.cshtml` | `hx-push-url="true"` pushes handler URL | Set `hx-push-url="/Courses/Detail?id={guid}"` with clean path |
| `_CourseCard.cshtml` | Title link and button both have HTMX + full-page attrs (redundant) | Keep both as dual-mode (HTMX primary, `href` fallback) |
| `Detail.cshtml.cs` | No server-side changes needed | N/A |
| `Detail.cshtml` | Page already renders correctly from `OnGetAsync` | No changes needed |
