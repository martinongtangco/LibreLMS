# Research: Clean Up Orphaned HTMX Handler

## Decision: `OnGetDetailAsync` is confirmed orphaned

**Finding**: `grep -rn "OnGetDetailAsync" src/` returns exactly 1 hit — the method definition itself at `src/Host/Pages/Courses/Detail.cshtml.cs:59`. No view, partial, JavaScript, or test references it.

**Rationale**: After spec 005's implementation removed all HTMX attributes from `_CourseCard.cshtml` (replacing them with plain `asp-page` tag helpers), the `OnGetDetailAsync` handler lost its only caller. The card no longer fires HTMX requests to `?handler=Detail`.

**Alternatives considered**:
1. **Keep the handler for future use** — rejected. Dead code misleads future developers. If HTMX inline swap is needed again, it can be re-added with a clear decision record.
2. **Redirect `?handler=Detail` to clean URL** — unnecessary. No user-facing path leads to this handler. Old bookmarks would 404 anyway (handler returns partial HTML), which is a reasonable failure mode for abandoned functionality.

## Decision: Spec 005 artifacts need annotation, not rewrite

**Finding**: Spec 005's `tasks.md` describes the originally planned fix (change `hx-push-url` value) but the actual implementation took a simpler approach (remove HTMX entirely). The spec's US4 (HTMX inline swap) was implicitly abandoned.

**Rationale**: Full rewrites of completed specs are unnecessary and lose historical context. Annotations (superseded markers, decision notes) preserve the record while clarifying current state.

## Decision: Spec 004 is out of scope

**Finding**: Spec 004 (`004-htmx-razor-conversion`) task T022 created the `OnGetDetailAsync` handler. Its tasks.md references the handler. However, spec 004 is a separate completed slice and its artifacts should not be modified by this cleanup.

**Rationale**: Cross-spec consistency is desirable but out of scope. If spec 004's artifacts are misleading, that spec gets its own follow-up cleanup. Modifying completed specs as a side effect violates the "sliced thin" principle — each spec should be self-contained.

**Action**: Note the cross-spec inconsistency in this spec's edge cases but do not modify spec 004.
