using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public CourseIndexModel(
        CourseCatalogService catalogService,
        IEnrollmentLookup enrollmentLookup,
        CourseVisibilityService visibilityService)
    {
        _catalogService = catalogService;
        _enrollmentLookup = enrollmentLookup;
        _visibilityService = visibilityService;
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
        int page = 1)
    {
        // Trim search term
        search = search?.Trim();
        if (string.IsNullOrWhiteSpace(search))
            search = null;

        // If search or category is being sent (meaning they changed), reset to page 1
        // If only page changed (no search/category params or empty), use provided page
        var effectivePage = page;

        // Cap page number to valid range
        // First get total count to determine max page
        var browseResult = await _catalogService.BrowseAsync(search, category, 1, PageSize);
        var totalPages = browseResult.TotalCount > 0
            ? (int)Math.Ceiling((double)browseResult.TotalCount / PageSize)
            : 1;

        effectivePage = Math.Max(1, Math.Min(page, totalPages));

        // If search or category changed, force page 1
        if (search != null || category != null)
        {
            // When search or category is in the request, we're filtering — reset to page 1
            // But we need to differentiate: if the user is paginating (no filter change),
            // they should keep their page. The way to tell: if the request includes
            // a page-reset hidden field set to "1", it means the user changed a filter.
            // HTMX will send page=1 for filter changes (from hidden field) and actual page for pagination.
            // We'll let the caller handle this: if page is explicitly 1, use it; otherwise use the capped value.
            if (page == 1)
                effectivePage = 1;
            else
                effectivePage = Math.Max(1, Math.Min(page, totalPages));
        }

        var result = await GetPagedCourses(search, category, effectivePage, PageSize);

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
        HashSet<Guid> enrolledIds = new();
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
            // Authenticated user with org — get visible course IDs first
            var visible = await _visibilityService.GetVisibleCoursesAsync(orgId.Value);
            var visibleCourseIds = visible.Select(v => v.CourseId).ToHashSet();

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

        // Check enrollment status for each course
        foreach (var item in browseResult.Items)
        {
            if (await _enrollmentLookup.IsEnrolledAsync(studentId, item.Id))
            {
                enrolledIds.Add(item.Id);
            }
        }

        // Map CourseItemDto to CourseItem (with enrollment status)
        var courseItems = browseResult.Items.Select(c =>
            new CourseItem(c.Id, c.Title, c.ShortDescription, c.Category, c.Duration,
                enrolledIds.Contains(c.Id))).ToList();

        return new BrowseResultWithEnrollments(courseItems, browseResult.TotalCount, browseResult.PageNumber, browseResult.PageSize);
    }

    /// <summary>Get distinct categories from all visible courses (for the dropdown).</summary>
    private async Task<List<string>> GetCategoriesAsync()
    {
        var role = HttpContext.User.Identity?.IsAuthenticated == true
            ? HttpContext.User.FindFirstValue(ClaimTypes.Role)
            : null;
        var orgId = role is not null
            ? AuthHelpers.GetCurrentUserOrgId(HttpContext.User)
            : null;

        IEnumerable<LibreLms.Modules.Catalog.Domain.Course> courses;

        if (orgId.HasValue)
        {
            var visible = await _visibilityService.GetVisibleCoursesAsync(orgId.Value);
            var visibleCourseIds = visible.ToDictionary(v => v.CourseId);
            var allCourses = await _catalogService.ListAsync();
            courses = allCourses.Where(c => visibleCourseIds.ContainsKey(c.Id));
        }
        else
        {
            courses = await _catalogService.ListAsync();
        }

        return courses.Select(c => c.Category).Distinct().OrderBy(c => c).ToList();
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
