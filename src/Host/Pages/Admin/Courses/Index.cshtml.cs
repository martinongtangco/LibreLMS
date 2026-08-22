using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Contracts.Management;
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
    private readonly IOrganizationLookup _orgLookup;

    public IndexModel(
        CourseCatalogService catalogService,
        CourseVisibilityService visibilityService,
        ScormPackageService scormService,
        IOrganizationLookup orgLookup)
    {
        _catalogService = catalogService;
        _visibilityService = visibilityService;
        _scormService = scormService;
        _orgLookup = orgLookup;
    }

    public List<CourseDisplay> Courses { get; set; } = new();
    public Dictionary<Guid, bool> HasScormPerCourse { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public int TotalCount { get; set; } = 0;
    public int TotalPages { get; set; } = 1;
    public LibreLms.Host.Pages.Admin.AdminPaginationModel? Pagination { get; set; }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string SortBy { get; set; } = "title";
    [BindProperty(SupportsGet = true)] public string SortDirection { get; set; } = "asc";
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = AdminPageState.DefaultPageSize;

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
            await LoadAsync(stepBackWhenEmpty: false);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load courses: {ex.Message}";
        }
    }

    private async Task LoadAsync(bool stepBackWhenEmpty)
    {
        // Trim search term
        Search = Search?.Trim();
        if (string.IsNullOrWhiteSpace(Search))
            Search = null;

        if (string.IsNullOrWhiteSpace(Category))
            Category = null;

        // Allowlist the sort values in C#; the sorting itself happens server-side in
        // the BrowseAsync stored procedure (the old in-page-only sort was the bug)
        SortBy = SortBy.ToLowerInvariant() switch
        {
            "category" => "category",
            "duration" => "duration",
            _ => "title"
        };
        SortDirection = SortDirection.ToLowerInvariant() == "desc" ? "desc" : "asc";

        // Allowlist page size {10,30,50,100}; anything else falls back to the default
        PageSize = AdminPageState.NormalizePageSize(PageSize);

        var page = Math.Max(1, PageNumber);

        // Use BrowseAsync for server-side pagination with search, category filter, and sort
        var browseResult = await _catalogService.BrowseAsync(
            Search, Category, page, PageSize, null, SortBy, SortDirection);

        // Clamp to the last valid page and re-fetch if the requested page is out of range
        var effective = AdminPageState.ClampPage(page, browseResult.TotalCount, PageSize);
        if (effective != page)
        {
            browseResult = await _catalogService.BrowseAsync(
                Search, Category, effective, PageSize, null, SortBy, SortDirection);
        }

        // After a row action (delete): if the current page came back empty and we are past
        // page 1, step back one page and re-clamp (contract interaction rule 6)
        if (stepBackWhenEmpty && browseResult.Items.Count() == 0 && effective > 1)
        {
            var previous = AdminPageState.ClampPage(effective - 1, browseResult.TotalCount, PageSize);
            browseResult = await _catalogService.BrowseAsync(
                Search, Category, previous, PageSize, null, SortBy, SortDirection);
            effective = AdminPageState.ClampPage(previous, browseResult.TotalCount, PageSize);
        }

        // Resolve org names for this page's distinct OrganizationIds via the Management
        // contract, cached per page (missing org -> "Unknown")
        var orgNames = new Dictionary<Guid, string>();
        foreach (var orgId in browseResult.Items.Select(i => i.OrganizationId).Distinct())
        {
            var org = await _orgLookup.GetOrganizationAsync(orgId);
            orgNames[orgId] = org?.Name ?? "Unknown";
        }

        var items = browseResult.Items.ToList();

        // Build SCORM lookup for all course IDs in this page
        var scormLookup = new Dictionary<Guid, bool>();
        foreach (var item in items)
        {
            var hasScorm = await _scormService.GetPackageByCourseIdAsync(item.Id);
            scormLookup[item.Id] = hasScorm != null;
        }
        HasScormPerCourse = scormLookup;

        Courses = items.Select(item =>
            new CourseDisplay(
                item.Id,
                item.Title,
                item.Category,
                orgNames.TryGetValue(item.OrganizationId, out var orgName) ? orgName : "Unknown",
                "Local",
                "Visible",
                scormLookup.TryGetValue(item.Id, out var hasScorm) && hasScorm
            )
        ).ToList();

        // Get distinct categories for dropdown
        Categories = await GetCategoriesAsync();

        // Expose the effective page state and build the shared pagination model
        TotalCount = browseResult.TotalCount;
        TotalPages = AdminPageState.TotalPages(TotalCount, PageSize);
        PageNumber = effective;
        Pagination = new LibreLms.Host.Pages.Admin.AdminPaginationModel(
            Page: effective,
            TotalPages: TotalPages,
            PageSize: PageSize,
            Total: TotalCount,
            ActionUrl: "/Admin/Courses/Index",
            FilterQueryParams: new[]
            {
                new KeyValuePair<string, string?>("search", Search),
                new KeyValuePair<string, string?>("category", Category),
                new KeyValuePair<string, string?>("sortBy", SortBy),
                new KeyValuePair<string, string?>("sortDirection", SortDirection)
            },
            BuildPageUrl: pageNumber =>
                $"/Admin/Courses/Index?pageNumber={pageNumber}" +
                $"&pageSize={PageSize}" +
                $"&search={Uri.EscapeDataString(Search ?? string.Empty)}" +
                $"&category={Uri.EscapeDataString(Category ?? string.Empty)}" +
                $"&sortBy={Uri.EscapeDataString(SortBy)}" +
                $"&sortDirection={Uri.EscapeDataString(SortDirection)}"
        );
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
        }
        catch (KeyNotFoundException)
        {
            Error = "Course not found.";
        }

        try
        {
            await LoadAsync(stepBackWhenEmpty: true);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load courses: {ex.Message}";
        }

        return Page();
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
