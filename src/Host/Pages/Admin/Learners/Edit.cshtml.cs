using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using LibreLms.Modules.Management.Application;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Admin.Learners;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class EditModel : PageModel
{
    private readonly UserService _userService;
    private readonly OrganizationService _orgService;

    public EditModel(UserService userService, OrganizationService orgService)
    {
        _userService = userService;
        _orgService = orgService;
    }

    [BindProperty]
    public EditLearnerInput Input { get; set; } = new();

    public SelectList? Orgs { get; set; }
    public SelectList? Roles { get; set; }
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (!Guid.TryParse(id, out var userId))
            return NotFound();

        var user = await _userService.GetByIdAsync(userId);
        if (user is null)
            return NotFound();

        Input = new EditLearnerInput
        {
            Id = user.Id.ToString(),
            Name = user.Name,
            Email = user.Email,
            OrganizationId = user.OrganizationId.ToString(),
            Role = user.Role
        };

        var allOrgs = await _orgService.ListAllAsync();
        Orgs = new SelectList(
            allOrgs.Select(o => new SelectListItem(o.Name, o.Id.ToString())),
            "Value", "Text", Input.OrganizationId);

        var roleItems = new List<SelectListItem>
        {
            new SelectListItem("Super User", RoleNames.SuperUser),
            new SelectListItem("Organization Admin", RoleNames.OrgAdmin),
            new SelectListItem("Learner", RoleNames.Learner)
        };
        Roles = new SelectList(roleItems, "Value", "Text", Input.Role);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Guid.TryParse(Input.Id, out var userId))
            return NotFound();

        try
        {
            if (string.IsNullOrWhiteSpace(Input.Name))
            {
                Error = "Name is required.";
                await OnGetAsync(Input.Id);
                return Page();
            }

            var orgId = string.IsNullOrEmpty(Input.OrganizationId) ? (Guid?)null : Guid.Parse(Input.OrganizationId);
            var role = string.IsNullOrEmpty(Input.Role) ? null : Input.Role;

            await _userService.UpdateAsync(userId, Input.Name, role, orgId);
            SuccessMessage = "Learner updated successfully.";
            return RedirectToPage("Index");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            await OnGetAsync(Input.Id);
            return Page();
        }
        catch (ArgumentException ex)
        {
            Error = ex.Message;
            await OnGetAsync(Input.Id);
            return Page();
        }
    }
}

public class EditLearnerInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
