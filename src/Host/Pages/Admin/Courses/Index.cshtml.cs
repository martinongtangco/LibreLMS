using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Scorm.Application;

namespace LibreLms.Host.Pages.Admin.Courses;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class IndexModel : PageModel
{
    private readonly CourseCatalogService _catalogService;
    private readonly CourseVisibilityService _visibilityService;
    private readonly ScormPackageService _scormService;

    public IndexModel(
        CourseCatalogService catalogService,
        CourseVisibilityService visibilityService,
        ScormPackageService scormService)
    {
        _catalogService = catalogService;
        _visibilityService = visibilityService;
        _scormService = scormService;
    }

    public List<CourseDisplay> Courses { get; set; } = new();
    public Dictionary<Guid, bool> HasScormPerCourse { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public int TotalCount { get; set; } = 0;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string SortBy { get; set; } = "title";
    [BindProperty(SupportsGet = true)] public string SortDirection { get; set; } = "asc";
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;

    [BindProperty(SupportsGet = true)] public string? Success { get; set; }
    [BindProperty(SupportsGet = true)] public string? ErrorParam { get; set; }

    public async Task OnGetAsync()
    {
        // Handle query-string messages from redirects
        if (Success == "true")
            SuccessMessage = "Course saved successfully.";
        if (!string.IsNullOrEmpty(ErrorParam))
            Error = ErrorParam;

        try
        {
            // Trim search term
            Search = Search?.Trim();
            if (string.IsNullOrWhiteSpace(Search))
                Search = null;

            if (string.IsNullOrWhiteSpace(Category))
                Category = null;

            if (PageNumber < 1)
                PageNumber = 1;

            // Use BrowseAsync for server-side pagination with search and category filter
            var browseResult = await _catalogService.BrowseAsync(
                Search, Category, PageNumber, PageSize);

            // Map to CourseDisplay with sort and pagination
            var items = browseResult.Items.ToList();
            TotalCount = browseResult.TotalCount;

            // Apply in-memory sorting since BrowseAsync returns pre-paged results
            items = ApplySorting(items);

            // For org names, use GetAllCoursesAsync which now resolves them properly
            var allCoursesWithOrgs = await _visibilityService.GetAllCoursesAsync();
            var orgLookup = allCoursesWithOrgs.ToDictionary(c => c.CourseId);

            // Build SCORM lookup for all course IDs in this page
            var courseIds = items.Select(i => i.Id).ToList();
            var scormLookup = new Dictionary<Guid, bool>();
            foreach (var courseId in courseIds)
            {
                var hasScorm = await _scormService.GetPackageByCourseIdAsync(courseId);
                scormLookup[courseId] = hasScorm != null;
            }
            HasScormPerCourse = scormLookup;

            Courses = items.Select(item =>
            {
                var hasOrg = orgLookup.TryGetValue(item.Id, out var orgInfo);
                return new CourseDisplay(
                    item.Id,
                    item.Title,
                    item.Category,
                    hasOrg ? orgInfo?.OwningOrganizationName ?? "Unknown" : "Unknown",
                    "Local",
                    "Visible",
                    scormLookup.TryGetValue(item.Id, out var hasScorm) && hasScorm
                );
            }).ToList();

            // Get distinct categories for dropdown
            Categories = await GetCategoriesAsync();
        }
        catch (Exception ex)
        {
            Error = $"Failed to load courses: {ex.Message}";
        }
    }

    private List<CourseItemDto> ApplySorting(List<CourseItemDto> items)
    {
        var isAscending = SortDirection.ToLowerInvariant() != "desc";

        return SortBy.ToLowerInvariant() switch
        {
            "category" => items.OrderBy(c => c.Category).ThenBy(c => c.Title).ToList(),
            "duration" => items.OrderBy(c => c.Duration).ThenBy(c => c.Title).ToList(),
            _ => isAscending
                ? items.OrderBy(c => c.Title).ToList()
                : items.OrderByDescending(c => c.Title).ToList()
        };
    }

    private async Task<List<string>> GetCategoriesAsync()
    {
        var allCourses = await _catalogService.ListAsync();
        return allCourses.Select(c => c.Category).Distinct().OrderBy(c => c).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid courseId)
    {
        try
        {
            await _visibilityService.DeleteCourseAsync(courseId);
            SuccessMessage = "Course deleted successfully.";
            await OnGetAsync();
            return Page();
        }
        catch (KeyNotFoundException)
        {
            Error = "Course not found.";
            await OnGetAsync();
            return Page();
        }
    }
}

public record CourseDisplay(
    Guid CourseId,
    string Title,
    string Category,
    string OrganizationName,
    string Source,
    string Visibility,
    bool HasScorm = false
);
