using System.Security.Claims;
using LibreLms.Contracts.Management;
using LibreLms.SharedKernel;

namespace LibreLms.Host.ManagementAuth;

/// <summary>
/// Helper methods for extracting authentication context from the current user.
/// Used in Razor Pages and minimal API endpoints.
/// </summary>
public static class AuthHelpers
{
    /// <summary>Get the current user's primary organization ID from claims.</summary>
    public static Guid? GetCurrentUserOrgId(ClaimsPrincipal user)
    {
        var orgIdStr = user.FindFirstValue(OrgClaimTypes.OrganizationId);
        return Guid.TryParse(orgIdStr, out var orgId) ? orgId : null;
    }

    /// <summary>Get the current user's role from claims.</summary>
    public static string GetCurrentUserRole(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    /// <summary>Check if the current user is a SuperUser.</summary>
    public static bool IsSuperUser(ClaimsPrincipal user)
    {
        return user.IsInRole(RoleNames.SuperUser);
    }

    /// <summary>Check if the current user is an OrgAdmin.</summary>
    public static bool IsOrgAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole(RoleNames.OrgAdmin);
    }

    /// <summary>
    /// Check if the current user has access to the target organization.
    /// SuperUser always has access. OrgAdmin has access if target is in their subtree.
    /// </summary>
    public static async Task<bool> IsInOrgSubtree(
        ClaimsPrincipal user,
        IOrganizationLookup orgLookup,
        Guid targetOrgId)
    {
        if (IsSuperUser(user))
            return true;

        var userOrgId = GetCurrentUserOrgId(user);
        if (!userOrgId.HasValue)
            return false;

        return await OrgScopeExtensions.IsInSubtreeAsync(orgLookup, userOrgId.Value, targetOrgId);
    }
}
