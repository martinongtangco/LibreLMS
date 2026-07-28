# Research: HTMX + Razor Integration

**Feature**: 004-htmx-razor-conversion
**Date**: 2025-07-28

## Decision 1: HTMX Version and Loading Strategy

**Decision**: Use HTMX 2.x (latest stable) loaded from `cdn.jsdelivr.net`

**Rationale**:
- HTMX 2.x is the current major version with active development and best browser support
- CDN loading avoids adding a NuGet package or build-step dependency — aligns with Constitution Principle II (no unnecessary layers)
- `cdn.jsdelivr.net` has strong reliability and global edge caching
- No local copy needed; if offline support is ever required, the file is trivially downloadable

**Alternatives considered**:
- **Local copy in `wwwroot/`**: Adds maintenance burden (manual updates). Only warranted if air-gapped deployment is needed.
- **HTMX 1.x**: Older, still functional, but 2.x has better `hx-on` event handling and swap options that simplify the implementation.
- **Hypermedia HTMX extensions (hyperscript, etc.)**: Not needed. Standard HTMX covers all 5 user stories without extensions.

**CDN URL**: `<script src="https://cdn.jsdelivr.net/npm/htmx.org@2.0.4/dist/htmx.min.js"></script>`

---

## Decision 2: HTMX Endpoint Strategy — Razor Handlers vs API Endpoints

**Decision**: Add new Razor Page handler methods that render partial views, using application services directly (not via HttpClient → API)

**Rationale**:
- The existing Minimal API endpoints (`/api/courses`, `/api/enrollments`) return JSON. HTMX swaps need HTML fragments.
- Razor Page handlers (`OnGetCourseListAsync`, `OnPostEnrollAsync`) can return `PartialViewResult` directly, rendering `.cshtml` partials.
- Services (CourseCatalogService, EnrollmentService) are already DI-registered and can be injected into page models — no need to go through HttpClient.
- This is simpler than teaching the API endpoints to detect `HX-Request` headers and return HTML, which would blur the JSON API boundary.
- Constitution Principle II (Clean Architecture) — presentation layer decides how to render; application services stay pure.

**Alternatives considered**:
- **Add `HX-Request` detection to existing API endpoints**: Would require API endpoints to return HTML, violating the API's JSON contract. Unnecessary complexity.
- **Use HTMX `hx-headers` to request JSON, then client-side render**: Defeats the purpose of HTMX (server-rendered HTML). Would need a client-side template engine.
- **Create separate Razor Pages for each HTMX target**: More file management overhead than handler methods on existing pages. Handler methods are the Razor Pages idiomatic approach for HTMX.

**Pattern**:
```csharp
// In Index.cshtml.cs
public async Task<PartialViewResult> OnGetCourseListAsync(string? search, string? category)
{
    var courses = await _catalogService.ListAsync(search, category);
    var enrolledIds = await GetEnrolledCourseIds();
    var model = courses.Select(c => new CourseItemDto(c, enrolledIds.Contains(c.Id)));
    return Partial("_CourseList", model);
}
```

---

## Decision 3: Debounce Strategy for Search

**Decision**: Use HTMX's built-in `hx-trigger="keyup changed delay:300ms"` for search input

**Rationale**:
- HTMX's `delay` modifier on `hx-trigger` handles debouncing natively — no custom JavaScript needed
- 300ms is the standard debounce window for search inputs; balances responsiveness with request reduction
- The `changed` modifier prevents firing on the first load or when the value hasn't actually changed
- This satisfies FR-003 (debounce) with zero custom code

**Alternatives considered**:
- **Custom JavaScript debounce**: Unnecessary — HTMX handles this declaratively.
- **Submit button only (no live search)**: Less UX-friendly. The spec asks for automatic updates on typing.
- **Longer delay (500ms+)**: Feels sluggish for a catalog that likely has <100 courses.

---

## Decision 4: Partial View Structure

**Decision**: Create reusable partial views under `Pages/Shared/` with strongly-typed view models as `record` types in the page code-behind files

**Rationale**:
- Razor Pages convention places shared partials under `Pages/Shared/`
- Strongly-typed partials (`@model CourseItem`) prevent runtime errors and enable IntelliSense
- `record` types as view models are lightweight and testable — no separate DTO project needed (these are presentation-layer concerns)
- Each partial represents a single swappable region, matching the HTMX `hx-target` pattern

**Partial views to create**:
| Partial | Purpose | HTMX Target For |
|---------|---------|-----------------|
| `_CourseList.cshtml` | Course card grid | Search/filter swaps, back-from-detail |
| `_CourseCard.cshtml` | Individual course card | Included by `_CourseList` |
| `_EnrollmentResult.cshtml` | Enroll button → status swap | Enrollment P1 action |
| `_MyCourseRow.cshtml` | Single enrollment row | Included by MyCourses partial |
| `_EnrollmentList.cshtml` | Full enrollments table | MyCourses refresh |

---

## Decision 5: Graceful Degradation (FR-008)

**Decision**: All HTMX-enhanced elements fall back to standard HTML form submissions and link navigation when JS is disabled

**Rationale**:
- `<form hx-get="..." method="get" action="...">` — if HTMX is absent, the `method` + `action` provide full-page navigation
- `<a hx-get="..." href="/Courses/Detail?id=...">` — if HTMX is absent, the `href` provides full-page navigation
- `<button hx-post="..." formaction="...">` — if HTMX is absent, the form submits normally
- This is HTMX's core design principle and requires no extra code

**Alternatives considered**:
- **Progressive enhancement with `<noscript>` fallbacks**: Overkill — standard HTML attributes already provide the fallback.
- **Server-side detection of JS support**: Adds complexity for a fallback path that rarely triggers in modern browsers.

---

## Decision 6: Error Handling in HTMX Swaps (FR-011)

**Decision**: Return a small error partial view (`_ErrorPartial.cshtml`) on API/service failures during HTMX requests; detect via `Request.Headers["HX-Request"]`

**Rationale**:
- When HTMX makes a request, it sets the `HX-Request: true` header
- Page handlers can check this header and return a targeted error partial instead of the full error page
- The error partial renders inline within the HTMX target region, keeping the navbar and layout intact
- Standard ASP.NET Core error handling (`UseExceptionHandler`) remains for non-HTMX requests

**Alternatives considered**:
- **HTMX `hx-on::error` client-side handling**: Less reliable — depends on JavaScript working. Server-side detection is more robust.
- **Throw exceptions and let the global error handler run**: Would show the full error page, breaking the SPA feel.

---

## Summary of Resolved Unknowns

| Original Unknown | Resolution |
|-----------------|------------|
| HTMX version/loading | v2.x from cdn.jsdelivr.net |
| Endpoint strategy | Razor Page handlers with PartialViewResult, direct service injection |
| Debounce approach | HTMX `hx-trigger="keyup changed delay:300ms"` |
| Partial view structure | `Pages/Shared/` with strongly-typed record models |
| Graceful degradation | Standard HTML `action`/`href` attributes as fallbacks |
| Error handling | `HX-Request` header detection → error partial view |
