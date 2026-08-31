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
        var result = await GetPagedCourses(Search, Category, PageNumber, PageSize);

        Courses = result.Items;
        TotalCount = result.TotalCount;
        // Derive categories from the full filtered result set for the dropdown
        Categories = await GetCategoriesAsync();
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
        var result = await GetPagedCourses(search, category, requestedPage, PageSize);

        var effectivePage = requestedPage;
        if (requestedPage > 1 && result.Items.Count == 0 && result.TotalCount > 0)
        {
            var totalPages = (int)Math.Ceiling((double)result.TotalCount / PageSize);
            effectivePage = Math.Min(requestedPage, totalPages);
            result = await GetPagedCourses(search, category, effectivePage, PageSize);
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
    private async Task<BrowseResultWithEnrollments> GetPagedCourses(string? search, string? category, int pageNumber, int pageSize)
    {
        var studentId = ScormHelpers.GetStudentId(HttpContext);
        var enrolledIds = new HashSet<Guid>();
        BrowseResult browseResult;

        // Check if user is authenticated with org context
        var role = HttpContext.User.Identity?.IsAuthenticated == true
            ? HttpContext.User.FindFirstValue(ClaimTypes.Role)
            : null;
        var orgId = role is not null
            ? AuthHelpers.GetCurrentUserOrgId(HttpContext.User)
            : null;

        if (orgId.HasValue)
        {
            // Authenticated user with org — get visible course IDs first.
            // Courses the org admin marked hidden (IsHidden) are excluded from the
            // browse filter — spec 009 scenario 5 / bug-047.
            var visible = await _visibilityService.GetVisibleCoursesAsync(orgId.Value);
            var visibleCourseIds = visible.Where(v => !v.IsHidden).Select(v => v.CourseId).ToHashSet();

            // Call stored procedure; filter by visible IDs in C# (avoids TVP complexity)
            browseResult = await _catalogService.BrowseAsync(
                search, category, pageNumber, pageSize,
                visibleCourseIds);
        }
        else
        {
            // Unauthenticated or no org — show all courses
            browseResult = await _catalogService.BrowseAsync(
                search, category, pageNumber, pageSize);
        }

        // One bulk enrollment check for the whole page (spec 048 E1) — replaces the
        // per-row IsEnrolledAsync loop; membership is a HashSet lookup below.
        var pageCourseIds = browseResult.Items.Select(c => c.Id).ToList();
        if (pageCourseIds.Count > 0)
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
    /// Get distinct categories for the dropdown (spec 048 E3):
    /// org-scoped users derive them from the already-fetched visible course DTOs
    /// (zero extra queries; hidden courses stay excluded — spec 009 scenario 5 / bug-047);
    /// everyone else gets one SELECT DISTINCT via the Catalog contract.
    /// </summary>
    private async Task<List<string>> GetCategoriesAsync()
    {
        var role = HttpContext.User.Identity?.IsAuthenticated == true
            ? HttpContext.User.FindFirstValue(ClaimTypes.Role)
            : null;
        var orgId = role is not null
            ? AuthHelpers.GetCurrentUserOrgId(HttpContext.User)
            : null;

        if (orgId.HasValue)
        {
            var visible = await _visibilityService.GetVisibleCoursesAsync(orgId.Value);
            return visible
                .Where(v => !v.IsHidden)
                .Select(v => v.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        return (await _courseLookup.GetDistinctCategoriesAsync()).ToList();
    }
}

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
