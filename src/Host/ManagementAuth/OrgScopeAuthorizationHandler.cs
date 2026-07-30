using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using LibreLms.Contracts.Management;
using LibreLms.SharedKernel;

namespace LibreLms.Host.ManagementAuth;

/// <summary>
/// Evaluates org-scope authorization: SuperUser gets full access;
/// OrgAdmin gets access to their org and all descendants;
/// Learner gets no admin access.
/// </summary>
public class OrgScopeAuthorizationHandler(IOrganizationLookup orgLookup) : AuthorizationHandler<RequireOrgScopeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireOrgScopeRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        var userOrgIdStr = context.User.FindFirstValue(OrgClaimTypes.OrganizationId);

        if (!Guid.TryParse(userOrgIdStr, out var userOrgId))
        {
            return; // Cannot determine user's org scope — deny
        }

        // SuperUser has unrestricted access
        if (role == RoleNames.SuperUser)
        {
            context.Succeed(requirement);
            return;
        }

        // Learner cannot perform admin operations
        if (role == RoleNames.Learner)
        {
            return;
        }

        // OrgAdmin: check if target org is in their subtree
        if (role == RoleNames.OrgAdmin && requirement.TargetOrgId.HasValue)
        {
            var ancestorIds = await orgLookup.GetAncestorOrgIdsAsync(requirement.TargetOrgId.Value);

            // User has access if the target org is the user's org or a descendant
            // i.e., the user's org is an ancestor of the target org
            if (ancestorIds.Contains(userOrgId))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
