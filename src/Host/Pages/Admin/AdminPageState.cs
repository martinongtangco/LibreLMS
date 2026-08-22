namespace LibreLms.Host.Pages.Admin;

/// <summary>
/// Shared page-state math for the three admin list pages (Courses, Enrollments, Learners).
/// Page size is allowlisted to {10, 30, 50, 100} (anything else falls back to 10) and the
/// requested page number is clamped to 1..totalPages so a tampered URL always renders a
/// valid page (spec 032, contracts/admin-pages-query.md).
/// </summary>
public static class AdminPageState
{
    // The only page sizes the admin pages accept; anything else falls back to the default.
    public static readonly int[] AllowedPageSizes = { 10, 30, 50, 100 };
    public const int DefaultPageSize = 10;

    /// <summary>Allowlist {10,30,50,100}; any other value (999, 15, 0, negative, ...) resolves to 10.</summary>
    public static int NormalizePageSize(int requested)
        => AllowedPageSizes.Contains(requested) ? requested : DefaultPageSize;

    /// <summary>max(1, ceil(total / normalizedPageSize)); a zero/empty result set still yields 1 page.</summary>
    public static int TotalPages(int total, int pageSize)
    {
        var size = NormalizePageSize(pageSize);
        return Math.Max(1, (int)Math.Ceiling((double)total / size));
    }

    /// <summary>max(1, min(requested, TotalPages)); a tampered pageNumber=99999 renders the last page.</summary>
    public static int ClampPage(int requested, int totalCount, int pageSize)
    {
        var last = TotalPages(totalCount, pageSize);
        return Math.Max(1, Math.Min(requested, last));
    }
}

/// <summary>View model for the shared _AdminPagination partial (see Pages/Shared/_AdminPagination.cshtml).
/// BuildPageUrl returns the full URL for a given page number, carrying the page's filter/sort
/// params and the current page size. FilterQueryParams are re-emitted as hidden inputs by the
/// page-size form so a size change keeps active filters and resets to page 1.</summary>
public record AdminPaginationModel(
    int Page,
    int TotalPages,
    int PageSize,
    int Total,
    string ActionUrl,
    System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string?>> FilterQueryParams,
    System.Func<int, string> BuildPageUrl);
