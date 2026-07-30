using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using LibreLms.Modules.Management.Application;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Admin.Learners;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class CreateModel : PageModel
{
    private readonly UserService _userService;
    private readonly OrganizationService _orgService;

    public CreateModel(UserService userService, OrganizationService orgService)
    {
        _userService = userService;
        _orgService = orgService;
    }

    [BindProperty]
    public CreateLearnerInput Input { get; set; } = new();

    public SelectList? Orgs { get; set; }
    public SelectList? Roles { get; set; }
    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        var allOrgs = await _orgService.ListAllAsync();
        Orgs = new SelectList(
            allOrgs.Select(o => new SelectListItem(o.Name, o.Id.ToString())),
            "Value", "Text");

        var roleItems = new List<SelectListItem>
        {
            new SelectListItem("Learner", RoleNames.Learner),
            new SelectListItem("Organization Admin", RoleNames.OrgAdmin)
        };
        Roles = new SelectList(roleItems, "Value", "Text", RoleNames.Learner);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Input.Name))
            {
                Error = "Name is required.";
                await OnGetAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                Error = "Email is required.";
                await OnGetAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Input.Password))
            {
                Error = "Password is required.";
                await OnGetAsync();
                return Page();
            }

            if (!Guid.TryParse(Input.OrganizationId, out var orgId))
            {
                Error = "Organization is required.";
                await OnGetAsync();
                return Page();
            }

            var role = string.IsNullOrEmpty(Input.Role) ? RoleNames.Learner : Input.Role;
            var student = await _userService.CreateAsync(Input.Name, Input.Email, Input.Password, role, orgId);

            return RedirectToPage("Index");
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            await OnGetAsync();
            return Page();
        }
        catch (ArgumentException ex)
        {
            Error = ex.Message;
            await OnGetAsync();
            return Page();
        }
    }
}

public class CreateLearnerInput
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string Role { get; set; } = RoleNames.Learner;
}
