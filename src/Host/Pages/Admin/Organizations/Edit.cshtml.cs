using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Host.Pages.Admin.Organizations;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class EditModel : PageModel
{
    private readonly OrganizationService _service;

    public EditModel(OrganizationService service)
    {
        _service = service;
    }

    [BindProperty]
    public EditOrgInput Input { get; set; } = new();

    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (!Guid.TryParse(id, out var orgId))
            return NotFound();

        var org = await _service.GetByIdAsync(orgId);
        if (org is null)
            return NotFound();

        Input = new EditOrgInput { Id = org.Id.ToString(), Name = org.Name, Description = org.Description };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Guid.TryParse(Input.Id, out var orgId))
            return NotFound();

        try
        {
            if (string.IsNullOrWhiteSpace(Input.Name))
            {
                Error = "Organization name is required.";
                return Page();
            }

            var org = await _service.UpdateAsync(orgId, Input.Name, Input.Description);
            SuccessMessage = $"Organization '{org.Name}' updated successfully.";
            return RedirectToPage("Index");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}

public class EditOrgInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
