using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Endpoints;
using LibreLms.Host.ManagementAuth;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Admin.Organizations;

/// <summary>
/// Page model for the interactive organization chart view.
/// Renders an SVG tree chart with zoom, pan, and context menu actions.
/// </summary>
[Authorize(Roles = "SuperUser,OrgAdmin")]
public class ChartModel : PageModel
{
    private readonly OrganizationService _orgService;

    public ChartModel(OrganizationService orgService)
    {
        _orgService = orgService;
    }

    public IList<OrgChartNodeDto>? Nodes { get; set; }
    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Nodes = await LoadChartAsync();
        }
        catch (Exception ex)
        {
            Error = $"Failed to load organization chart: {ex.Message}";
        }
    }

    #region User Story 2 — Create Child Organization

    public async Task<IActionResult> OnGetCreateChildDialogAsync(Guid parentId)
    {
        var parent = await _orgService.GetByIdAsync(parentId, AuthHelpers.GetScope(User));
        if (parent is null)
            return NotFound();

        return Partial("_CreateChildDialog", new CreateChildViewModel { ParentId = parentId, ParentName = parent.Name });
    }

    public async Task<IActionResult> OnPostCreateChildAsync(Guid parentId, [Bind(Prefix = "Form")] CreateChildViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return Partial("_ErrorPartial", "Organization name is required.");

        try
        {
            await _orgService.CreateAsync(model.Name, model.Description, parentId, AuthHelpers.GetScope(User));
            Nodes = await LoadChartAsync();
            return Partial("_OrgChartSvg", Nodes);
        }
        catch (LibreLms.Contracts.Management.ForbiddenAccessException ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
    }

    #endregion

    #region User Story 3 — Edit / Disable / Enable

    public async Task<IActionResult> OnGetEditDialogAsync(Guid id)
    {
        var (org, userCount, courseCount) = await _orgService.GetByIdWithStatusAsync(id, AuthHelpers.GetScope(User));
        return Partial("_EditDialog", new EditOrgViewModel
        {
            Id = org.Id,
            Name = org.Name,
            Description = org.Description,
            IsDisabled = org.IsDisabled,
            UserCount = userCount,
            CourseCount = courseCount
        });
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, [Bind(Prefix = "Form")] EditOrgViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return Partial("_ErrorPartial", "Organization name is required.");

        try
        {
            await _orgService.UpdateAsync(id, model.Name, model.Description, AuthHelpers.GetScope(User));

            // Handle enable/disable toggle
            var current = await _orgService.GetByIdAsync(id, AuthHelpers.GetScope(User));
            if (current is not null)
            {
                if (model.IsDisabled && !current.IsDisabled)
                    await _orgService.DisableAsync(id, AuthHelpers.GetScope(User));
                else if (!model.IsDisabled && current.IsDisabled)
                    await _orgService.EnableAsync(id, AuthHelpers.GetScope(User));
            }

            Nodes = await LoadChartAsync();
            return Partial("_OrgChartSvg", Nodes);
        }
        catch (LibreLms.Contracts.Management.ForbiddenAccessException ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostDisableAsync(Guid id)
    {
        try
        {
            await _orgService.DisableAsync(id, AuthHelpers.GetScope(User));
            Nodes = await LoadChartAsync();
            return Partial("_OrgChartSvg", Nodes);
        }
        catch (LibreLms.Contracts.Management.ForbiddenAccessException ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostEnableAsync(Guid id)
    {
        try
        {
            await _orgService.EnableAsync(id, AuthHelpers.GetScope(User));
            Nodes = await LoadChartAsync();
            return Partial("_OrgChartSvg", Nodes);
        }
        catch (LibreLms.Contracts.Management.ForbiddenAccessException ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    #endregion

    #region User Story 4 — User Assignment

    public async Task<IActionResult> OnGetAddUserDialogAsync(Guid orgId)
    {
        var org = await _orgService.GetByIdAsync(orgId, AuthHelpers.GetScope(User));
        if (org is null) return NotFound();
        return Partial("_AddUserDialog", new AddUserViewModel { OrgId = orgId, OrgName = org.Name });
    }

    public async Task<IActionResult> OnPostCreateUserAsync(Guid orgId, [Bind(Prefix = "Form")] AddUserViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Email))
            return Partial("_ErrorPartial", "Name and email are required.");

        try
        {
            // Delegate to UserService via the API (direct service call would need UserService injection)
            // For now, return a success indicator — full implementation in Phase 6
            Nodes = await LoadChartAsync();
            return Partial("_OrgChartSvg", Nodes);
        }
        catch (Exception ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
    }

    public async Task<IActionResult> OnGetAssignUserDialogAsync(Guid orgId)
    {
        var org = await _orgService.GetByIdAsync(orgId, AuthHelpers.GetScope(User));
        if (org is null) return NotFound();
        return Partial("_AssignUserDialog", new AssignUserViewModel { OrgId = orgId, OrgName = org.Name });
    }

    public async Task<IActionResult> OnPostAssignUserAsync(Guid orgId, Guid userId)
    {
        try
        {
            // Full implementation in Phase 6
            Nodes = await LoadChartAsync();
            return Partial("_OrgChartSvg", Nodes);
        }
        catch (Exception ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
    }

    #endregion

    #region User Story 5 — Course Assignment

    public async Task<IActionResult> OnGetAssignCourseDialogAsync(Guid orgId)
    {
        var org = await _orgService.GetByIdAsync(orgId, AuthHelpers.GetScope(User));
        if (org is null) return NotFound();
        return Partial("_AssignCourseDialog", new AssignCourseViewModel { OrgId = orgId, OrgName = org.Name });
    }

    public async Task<IActionResult> OnPostAssignCourseAsync(Guid orgId, Guid courseId)
    {
        try
        {
            // Full implementation in Phase 7
            Nodes = await LoadChartAsync();
            return Partial("_OrgChartSvg", Nodes);
        }
        catch (Exception ex)
        {
            return Partial("_ErrorPartial", ex.Message);
        }
    }

    #endregion

    #region Helpers

    private async Task<IList<OrgChartNodeDto>> LoadChartAsync()
    {
        // ADR 0010: the service pins an OrgAdmin's chart to their own subtree;
        // SuperUser may optionally focus on a subtree root.
        return await _orgService.GetChartTreeAsync(null, AuthHelpers.GetScope(User));
    }

    #endregion

    #region View Models

    public record CreateChildViewModel
    {
        public Guid ParentId { get; set; }
        public string ParentName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public record EditOrgViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsDisabled { get; set; }
        public int UserCount { get; set; }
        public int CourseCount { get; set; }
    }

    public record AddUserViewModel
    {
        public Guid OrgId { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = RoleNames.Learner;
        public string Password { get; set; } = string.Empty;
    }

    public record AssignUserViewModel
    {
        public Guid OrgId { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public Guid? SelectedUserId { get; set; }
    }

    public record AssignCourseViewModel
    {
        public Guid OrgId { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public Guid? SelectedCourseId { get; set; }
    }

    #endregion
}
