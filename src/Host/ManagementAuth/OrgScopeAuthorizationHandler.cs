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
            // Cannot determine user's org scope — deny
            return;
        }

        // SuperUser has unrestricted access to everything
        if (role == RoleNames.SuperUser)
        {
            context.Succeed(requirement);
            return;
        }

        // Learner cannot perform admin operations — always deny
        if (role == RoleNames.Learner)
        {
            return;
        }

        // OrgAdmin: check if target org is in their subtree
        if (role == RoleNames.OrgAdmin)
        {
            if (!requirement.TargetOrgId.HasValue)
            {
                // No specific target org — grant access to user's own org scope
                context.Succeed(requirement);
                return;
            }

            // Check if the target org is the user's org or a descendant
            // by seeing if the user's org is an ancestor of the target org
            var targetAncestorIds = await orgLookup.GetAncestorOrgIdsAsync(requirement.TargetOrgId.Value);
            if (targetAncestorIds.Contains(userOrgId))
            {
                context.Succeed(requirement);
                return;
            }

            // Also check if target is the user's own org
            if (requirement.TargetOrgId.Value == userOrgId)
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
