# Contract: Browse Courses HTMX Endpoint

**Feature**: specs/019-course-search-pagination/spec.md  
**Date**: 2025-07-31

## Endpoint

### GET `/Courses/Index?handler=CourseList`

**Triggered by**: HTMX `hx-get` from search input, category dropdown, and pagination controls  
**Response Type**: Partial view (`_CourseList.cshtml` + pagination controls)  
**Target**: `#course-list` div (inner HTML swap)

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `search` | `string` | No | `""` | Search term (debounced, 300ms) |
| `category` | `string` | No | `""` | Category filter value |
| `page` | `int` | No | `1` | Page number (1-indexed) |

**Response**: HTML fragment containing:
1. Course card grid (`<div class="metric-cards">` with `_CourseCard` partials)
2. Empty state if no results (`<div class="card empty-state">`)
3. Pagination controls (`<nav class="pagination">` with Previous/Next buttons and page indicator)

**HTMX Attributes on Controls**:
- Search input: `hx-get`, `hx-trigger="keyup changed delay:300ms"`, `hx-target="#course-list"`, `hx-include`
- Category select: `hx-get`, `hx-trigger="change"`, `hx-target="#course-list"`, `hx-include`
- Pagination Previous: `hx-get`, `hx-trigger="click"`, `hx-target="#course-list"`, `hx-include`
- Pagination Next: `hx-get`, `hx-trigger="click"`, `hx-target="#course-list"`, `hx-include`, disabled when on last page

**Behavior Notes**:
- Any filter change (search, category) resets page to 1
- Pagination navigation preserves current search and category filters
- Empty state shown when zero courses match filters
- Loading indicator (`.htmx-indicator`) shown during requests
