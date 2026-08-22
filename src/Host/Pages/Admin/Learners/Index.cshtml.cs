using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Infrastructure;

namespace LibreLms.Host.Pages.Admin.Learners;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class IndexModel : PageModel
{
    private readonly UserService _userService;
    private readonly OrganizationService _orgService;

    public IndexModel(UserService userService, OrganizationService orgService)
    {
        _userService = userService;
        _orgService = orgService;
    }

    public List<UserDto> Users { get; set; } = new();
    public SelectList? OrgFilter { get; set; }
    public SelectList? RoleFilter { get; set; }
    public string? Search { get; set; }
    public string? SelectedOrg { get; set; }
    public string? SelectedRole { get; set; }
    public string? Error { get; set; }

    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = AdminPageState.DefaultPageSize;

    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public LibreLms.Host.Pages.Admin.AdminPaginationModel? Pagination { get; set; }

    public async Task OnGetAsync(string? search, string? org, string? role)
    {
        try
        {
            Search = search;
            SelectedOrg = org;
            SelectedRole = role;

            var allOrgs = await _orgService.ListAllAsync();
            var items = new List<SelectListItem> { new("All Organizations", "") };
            items.AddRange(allOrgs.Select(o => new SelectListItem(o.Name, o.Id.ToString())));
            OrgFilter = new SelectList(items, "Value", "Text", SelectedOrg);

            var roleItems = new[]
            {
                new SelectListItem("All Roles", ""),
                new SelectListItem("SuperUser", "SuperUser"),
                new SelectListItem("OrgAdmin", "OrgAdmin"),
                new SelectListItem("Learner", "Learner")
            }.ToList();
            RoleFilter = new SelectList(roleItems, "Value", "Text", SelectedRole);

            // Shared pagination load pattern (spec 032): normalize size, clamp page, re-fetch if clamped.
            var effectiveSize = AdminPageState.NormalizePageSize(PageSize);
            var searchValue = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var roleFilter = string.IsNullOrEmpty(role) ? null : role;
            var requestedPage = Math.Max(1, PageNumber);

            var page = await _userService.ListAllPagedAsync(searchValue, roleFilter, requestedPage, effectiveSize);
            var effectivePage = AdminPageState.ClampPage(requestedPage, page.TotalCount, effectiveSize);

            if (effectivePage != requestedPage)
                page = await _userService.ListAllPagedAsync(searchValue, roleFilter, effectivePage, effectiveSize);

            Users = page.Items.ToList();
            PageSize = effectiveSize;
            PageNumber = effectivePage;
            TotalCount = page.TotalCount;
            TotalPages = AdminPageState.TotalPages(page.TotalCount, effectiveSize);

            // Pagination links and the page-size form carry the current filter values.
            Pagination = new LibreLms.Host.Pages.Admin.AdminPaginationModel(
                PageNumber,
                TotalPages,
                PageSize,
                TotalCount,
                "/Admin/Learners/Index",
                new[]
                {
                    new KeyValuePair<string, string?>("search", Search),
                    new KeyValuePair<string, string?>("org", SelectedOrg),
                    new KeyValuePair<string, string?>("role", SelectedRole)
                },
                p => "/Admin/Learners/Index"
                    + "?search=" + Uri.EscapeDataString(Search ?? "")
                    + "&org=" + Uri.EscapeDataString(SelectedOrg ?? "")
                    + "&role=" + Uri.EscapeDataString(SelectedRole ?? "")
                    + "&pageSize=" + PageSize
                    + "&pageNumber=" + p);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load learners: {ex.Message}";
        }
    }
}
