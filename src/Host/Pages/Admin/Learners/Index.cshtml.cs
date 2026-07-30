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

            var allUsers = await _userService.ListAllAsync(string.IsNullOrEmpty(role) ? null : role);

            // Filter by search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLowerInvariant();
                allUsers = allUsers.Where(u => u.Name.ToLowerInvariant().Contains(term) || u.Email.ToLowerInvariant().Contains(term)).ToList();
            }

            Users = allUsers.ToList();
        }
        catch (Exception ex)
        {
            Error = $"Failed to load learners: {ex.Message}";
        }
    }
}
