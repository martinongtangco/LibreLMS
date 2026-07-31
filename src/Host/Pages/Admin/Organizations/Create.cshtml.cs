using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Host.Pages.Admin.Organizations;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class CreateModel : PageModel
{
    private readonly OrganizationService _service;
    private readonly IWebHostEnvironment _env;

    public CreateModel(OrganizationService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [BindProperty]
    public CreateOrgInput Input { get; set; } = new();

    public SelectList? ParentOrgs { get; set; }
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        var allOrgs = await _service.ListAllAsync();
        var items = new List<SelectListItem> { new("(No parent — root)", "") };
        items.AddRange(allOrgs.Select(o => new SelectListItem(o.Name, o.Id.ToString())));
        ParentOrgs = new SelectList(items, "Value", "Text");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Input.Name))
            {
                Error = "Organization name is required.";
                return Page();
            }

            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(Input.ParentId))
                Guid.TryParse(Input.ParentId, out var parsedParentId);

            var org = await _service.CreateAsync(Input.Name, Input.Description, parentId);
            SuccessMessage = $"Organization '{org.Name}' created successfully.";
            return RedirectToPage("Index");
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}

public class CreateOrgInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ParentId { get; set; }
}
