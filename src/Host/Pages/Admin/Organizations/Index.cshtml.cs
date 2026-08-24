using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Host.Pages.Admin.Organizations;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class IndexModel : PageModel
{
    private readonly OrganizationService _service;

    public IndexModel(OrganizationService service)
    {
        _service = service;
    }

    public List<OrgTreeNode> OrgTree { get; set; } = new();
    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var allOrgs = await _service.ListAllAsync();
            OrgTree = BuildTree(allOrgs, null, false);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load organizations: {ex.Message}";
        }
    }

    private List<OrgTreeNode> BuildTree(IEnumerable<LibreLms.Modules.Management.Domain.Organization> orgs, Guid? parentId, bool ancestorDisabled)
    {
        var children = orgs
            .Where(o => o.ParentId == parentId)
            .Select(o => new OrgTreeNode(
                o.Id,
                o.Name,
                o.Description,
                o.ParentId,
                o.IsDisabled || ancestorDisabled,
                BuildTree(orgs, o.Id, o.IsDisabled || ancestorDisabled)
            ))
            .ToList();

        return children;
    }
}

/// <summary>
/// Page-level view model for one node of the organizations tree.
/// </summary>
/// <param name="IsDisabled">Own flag OR any ancestor's flag — a disabled org's whole subtree renders disabled.</param>
public record OrgTreeNode(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentId,
    bool IsDisabled,
    List<OrgTreeNode> Children
);
