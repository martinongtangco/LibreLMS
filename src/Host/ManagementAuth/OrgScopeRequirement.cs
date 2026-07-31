using Microsoft.AspNetCore.Authorization;

namespace LibreLms.Host.ManagementAuth;

/// <summary>
/// Authorization requirement that checks whether the authenticated user has access
/// to the target organization based on their role and organizational subtree.
/// </summary>
public class RequireOrgScopeRequirement : IAuthorizationRequirement
{
    /// <summary>The organization ID being accessed. Null means system-wide access is needed.</summary>
    public Guid? TargetOrgId { get; }

    public RequireOrgScopeRequirement(Guid? targetOrgId = null)
    {
        TargetOrgId = targetOrgId;
    }
}
