using LibreLms.Contracts.Management;

namespace LibreLms.Modules.Management.Application;

/// <summary>
/// Org-scope helpers (ADR 0010). All scoped Management application services
/// route their scope logic through this class so the subtree rule has one
/// definition:
///
/// - SuperUser: system-wide (subtree expansion returns null = no filter).
/// - OrgAdmin: their organization and all descendants.
/// - None (fail-closed): matches nothing.
/// </summary>
public static class OrgSubtree
{
    /// <summary>
    /// Expand the scope to the set of organization ids it covers.
    /// SuperUser returns null (meaning "no restriction"); OrgAdmin returns
    /// its org plus all descendants; None returns an empty set.
    /// </summary>
    public static async Task<HashSet<Guid>?> GetSubtreeOrgIdsAsync(OrgScope scope, IOrganizationLookup orgLookup)
    {
        if (scope.IsSuperUser)
            return null;

        if (!scope.OrganizationId.HasValue)
            return new HashSet<Guid>();

        var ids = new HashSet<Guid> { scope.OrganizationId.Value };
        var queue = new Queue<Guid>();
        queue.Enqueue(scope.OrganizationId.Value);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var childIds = await orgLookup.GetChildOrgIdsAsync(current);
            foreach (var child in childIds)
            {
                if (ids.Add(child))
                    queue.Enqueue(child);
            }
        }

        return ids;
    }

    /// <summary>
    /// Check a single target organization against the scope.
    /// SuperUser: always true. OrgAdmin: the target is its own org or a
    /// descendant. None: false (fail-closed).
    /// </summary>
    public static async Task<bool> IsOrgInScopeAsync(OrgScope scope, IOrganizationLookup orgLookup, Guid targetOrgId)
    {
        if (scope.IsSuperUser)
            return true;

        if (!scope.OrganizationId.HasValue)
            return false;

        var rootId = scope.OrganizationId.Value;
        if (rootId == targetOrgId)
            return true;

        // GetAncestorOrgIdsAsync includes the org itself.
        var ancestors = await orgLookup.GetAncestorOrgIdsAsync(targetOrgId);
        return ancestors.Contains(rootId);
    }
}
