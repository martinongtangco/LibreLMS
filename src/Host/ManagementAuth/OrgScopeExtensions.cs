using LibreLms.Contracts.Management;

namespace LibreLms.Host.ManagementAuth;

/// <summary>
/// Helper methods for building ancestor paths and checking subtree membership.
/// Used by the authorization handler and Razor Pages.
/// </summary>
public static class OrgScopeExtensions
{
    /// <summary>
    /// Check if a target organization is within the user's organizational subtree.
    /// Returns true if targetOrgId equals userOrgId or if userOrgId is an ancestor of targetOrgId.
    /// </summary>
    public static async Task<bool> IsInSubtreeAsync(
        IOrganizationLookup orgLookup,
        Guid userOrgId,
        Guid targetOrgId)
    {
        // Quick check: same org
        if (userOrgId == targetOrgId)
            return true;

        // Check if userOrgId is an ancestor of targetOrgId
        var ancestorIds = await orgLookup.GetAncestorOrgIdsAsync(targetOrgId);
        return ancestorIds.Contains(userOrgId);
    }
}
