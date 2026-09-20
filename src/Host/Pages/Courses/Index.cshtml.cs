using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Host.ManagementAuth;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Management.Application;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Courses;

public class CourseIndexModel : PageModel
{
    private readonly CourseCatalogService _catalogService;
    private readonly IEnrollmentLookup _enrollmentLookup;
    private readonly CourseVisibilityService _visibilityService;
    private readonly ICourseLookup _courseLookup;

    public CourseIndexModel(
        CourseCatalogService catalogService,
        IEnrollmentLookup enrollmentLookup,
        CourseVisibilityService visibilityService,
        ICourseLookup courseLookup)
    {
        _catalogService = catalogService;
        _enrollmentLookup = enrollmentLookup;
        _visibilityService = visibilityService;
        _courseLookup = courseLookup;
    }

    public List<CourseItem> Courses { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public int TotalCount { get; set; } = 0;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    public async Task OnGetAsync()
    {
        // Spec 054: resolve the org's visible catalog ONCE and share it between
        // the list and the category dropdown (was: two full
        // GetVisibleCoursesAsync fetches per request).
        var visible = await ResolveVisibleCatalogAsync();

        var result = await GetPagedCourses(Search, Category, PageNumber, PageSize, visible?.CourseIds);

        Courses = result.Items;
        TotalCount = result.TotalCount;
        // Derive categories from the already-resolved visible set for the
        // dropdown (zero extra queries; hidden courses stay excluded —
        // spec 009 scenario 5 / bug-047).
        Categories = visible?.Categories
            ?? (await _courseLookup.GetDistinctCategoriesAsync()).ToList();
    }

    /// <summary>HTMX handler: return course list + pagination partial for inline swap.</summary>
    public async Task<PartialViewResult> OnGetCourseListAsync(
        string? search,
        string? category,
        [FromQuery] int page = 1)
    {
        // [FromQuery] is required: without it, ASP.NET Core infers the binding source
        // for an optional value-type parameter (int page = 1) as Form, so the page
        // query-string value sent by HTMX hx-get requests was never bound (bug 028).

        // Trim search term
        search = search?.Trim();
        if (string.IsNullOrWhiteSpace(search))
            search = null;

        // Cap the requested page to the valid range (1..totalPages).
        // Filter changes (search/category) arrive as page=1 because the search input and
        // category select include the hidden #page-reset field (name="page" value="1")
        // via hx-include — so filtering always restarts at page 1, while pagination
        // requests carry the actual target page in the query string.
        // Fetch the requested page directly — ONE stored-procedure call on the common
        // in-range path (spec 048 E2). The BrowseCourses SP tolerates out-of-range pages:
        // OFFSET/FETCH returns an empty set (no error) and the second result set still
        // carries the total. So an empty page with a nonzero total means "past the last
        // page" — clamp to the last page and re-fetch (2 calls, same as the old
        // probe-then-fetch flow).
        var requestedPage = Math.Max(1, page);
        // Spec 054: one visible-catalog resolution per request, reused by the
        // (rare) clamp re-fetch below.
        var visible = await ResolveVisibleCatalogAsync();
        var result = await GetPagedCourses(search, category, requestedPage, PageSize, visible?.CourseIds);

        var effectivePage = requestedPage;
        if (requestedPage > 1 && result.Items.Count == 0 && result.TotalCount > 0)
        {
            var totalPages = (int)Math.Ceiling((double)result.TotalCount / PageSize);
            effectivePage = Math.Min(requestedPage, totalPages);
            result = await GetPagedCourses(search, category, effectivePage, PageSize, visible?.CourseIds);
        }

        // Build combined model: courses + pagination info
        var model = new BrowseViewModel(
            result.Items,
            result.TotalCount,
            effectivePage,
            PageSize,
            search,
            category
        );

        return Partial("_CourseListWithPagination", model);
    }

    /// <summary>Get paginated courses using the T-SQL stored procedure.</summary>
    private async Task<BrowseResultWithEnrollments> GetPagedCourses(string? search, string? category, int pageNumber, int pageSize, HashSet<Guid>? visibleCourseIds)
    {
        var studentId = ScormHelpers.GetStudentId(HttpContext);
        var enrolledIds = new HashSet<Guid>();

        // The visible set (null = unauthenticated/no org → unfiltered) is
        // applied inside the BrowseCourses SP (spec 054, ADR 0012) — rows,
        // page size and total count all agree.
        var browseResult = await _catalogService.BrowseAsync(
            search, category, pageNumber, pageSize, visibleCourseIds);

        // One bulk enrollment check for the whole page (spec 048 E1) — replaces the
        // per-row IsEnrolledAsync loop; membership is a HashSet lookup below.
        var pageCourseIds = browseResult.Items.Select(c => c.Id).ToList();
        // Guests (Guid.Empty — ADR 0013) get an empty enrolled set without a query.
        if (studentId != Guid.Empty && pageCourseIds.Count > 0)
        {
            enrolledIds = (await _enrollmentLookup.GetEnrolledCourseIdsAsync(studentId, pageCourseIds)).ToHashSet();
        }

        // Map CourseItemDto to CourseItem (with enrollment status)
        var courseItems = browseResult.Items.Select(c =>
            new CourseItem(c.Id, c.Title, c.ShortDescription, c.Category, c.Duration,
                enrolledIds.Contains(c.Id))).ToList();

        return new BrowseResultWithEnrollments(courseItems, browseResult.TotalCount, browseResult.PageNumber, browseResult.PageSize);
    }

    /// <summary>
    /// The org's visible catalog, resolved ONCE per request (spec 054): the
    /// course ids (browse filter, applied inside the SP) and the distinct
    /// non-hidden categories (dropdown). Null when the user has no org scope
    /// (unauthenticated / no org claim) — callers then browse unfiltered.
    /// Courses the org admin marked hidden (IsHidden) are excluded —
    /// spec 009 scenario 5 / bug-047. ADR 0010: the read is scoped to the
    /// caller's own org subtree (orgId comes from the auth claim, so the
    /// check always passes).
    /// </summary>
    private async Task<VisibleCatalog?> ResolveVisibleCatalogAsync()
    {
        var role = HttpContext.User.Identity?.IsAuthenticated == true
            ? HttpContext.User.FindFirstValue(ClaimTypes.Role)
            : null;
        var orgId = role is not null
            ? AuthHelpers.GetCurrentUserOrgId(HttpContext.User)
            : null;

        if (orgId is not { } id)
            return null;

        var visible = await _visibilityService.GetVisibleCoursesAsync(id, LibreLms.Contracts.Management.OrgScope.ForOrgAdmin(id));
        var visibleCourses = visible.Where(v => !v.IsHidden).ToList();
        return new VisibleCatalog(
            visibleCourses.Select(v => v.CourseId).ToHashSet(),
            visibleCourses.Select(v => v.Category).Distinct().OrderBy(c => c).ToList());
    }
}

/// <summary>One org's visible catalog, resolved once per request (spec 054).</summary>
public record VisibleCatalog(HashSet<Guid> CourseIds, List<string> Categories);

/// <summary>ViewModel for the combined course list + pagination partial.</summary>
public record BrowseViewModel(
    List<CourseItem> Courses,
    int TotalCount,
    int PageNumber,
    int PageSize,
    string? Search,
    string? Category);

/// <summary>Internal result with enrollment status mapped.</summary>
public record BrowseResultWithEnrollments(
    List<CourseItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record CourseListResponse(IEnumerable<CourseItem> Courses);
public record CourseItem(Guid Id, string Title, string ShortDescription, string Category, string Duration, bool IsEnrolled = false);
