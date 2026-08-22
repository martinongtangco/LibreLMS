# Contract: Admin Page Query Strings & Controls (Admin List Pagination)

**Feature**: [../spec.md](../spec.md)

All three admin index pages (`/Admin/Courses/Index`, `/Admin/Enrollments/Index`,
`/Admin/Learners/Index`) share the pagination query contract. Navigation stays full-page GET
(ADR 0005 — no HTMX for navigation); no JavaScript persistence.

## Shared pagination parameters (all three pages)

| Param | Type | Default | Rules |
|---|---|---|---|
| `pageNumber` | int | 1 | `< 1` → 1; `> totalPages` → `totalPages` (clamped before render; links always emit the effective value) |
| `pageSize` | int | 10 | allowlisted to **10 / 30 / 50 / 100**; any other value (999, 15, 12, 0, negative, empty) → 10 |

Derived values rendered in the page indicator: `totalPages = max(1, ceil(totalCount /
pageSize))`, displayed as **"Page {effectivePage} of {totalPages} ({totalCount} total)"**.

## Page-specific filter/sort parameters (existing, unchanged)

| Page | Params |
|---|---|
| Courses | `search` (title contains), `category` (exact), `sortBy` (`title`\|`category`\|`duration`, default `title`), `sortDirection` (`asc`\|`desc`, default `asc`) |
| Enrollments | `student` (name contains), `course` (title contains) |
| Learners | `search` (name/email contains), `role` (exact), `org` (bound, currently unapplied — pre-existing gap, out of scope) |

## Interaction rules (testable)

1. **Filter/search/sort change → page 1**: every filter form submits a hidden
   `pageNumber=1` (the mechanism already used by the Courses page), so any criterion change
   restarts pagination.
2. **Page size change → page 1**: the page size `<select>` (options exactly 10/30/50/100,
   default 10, current value selected) submits the filter form on change with `pageNumber=1`.
3. **Pagination links preserve state**: Previous/Next (and Courses sort-header links) carry
   the current `pageSize` + all active filter/sort values.
4. **Boundary controls** (mirrors Browse Courses, spec 028): Previous is *hidden* on page 1;
   Next is *hidden* on the last page; the whole `<nav>` is hidden when `totalPages <= 1`.
5. **Empty results**: empty-state message renders; no pagination controls.
6. **After a row action** (cancel enrollment / delete course): the same filtered/sorted view
   is reloaded; if the current page is now empty and `effectivePage > 1`, the previous page is
   shown; if page 1 is empty, the empty state renders.
7. **Invalid page size in URL** (`pageSize=999`): page renders with size 10 and the selector
   shows 10. **Out-of-range page** (`pageNumber=99999`): the last valid page renders.

## Control markup (shared look across the three pages)

- Page size selector: `<select name="pageSize">` with options 10/30/50/100 inside each page's
  existing filter form (submits on change).
- Pagination `<nav>`: `← Previous` button/link, center indicator span, `Next →` button/link —
  same labels/classes as the existing Admin > Courses pagination nav (which is kept, minus its
  now-dead in-memory sorting).
- Indicator text format is identical on all three pages and matches Browse Courses.
