namespace LibreLms.Contracts.Management;

/// <summary>
/// The caller's organization scope for admin operations (ADR 0010).
///
/// SuperUser: system-wide (no subtree restriction).
/// OrgAdmin: restricted to their organization's subtree (the org and all
/// descendants). The Host builds this value from the auth claims
/// (Role + OrganizationId) at the boundary; Management application services
/// enforce it — lists filter to the subtree, single-target operations refuse
/// out-of-scope targets.
///
/// Fail-closed: a scope with no organization id matches nothing (a caller
/// whose org claim is missing/invalid cannot reach any org's rows).
/// </summary>
public sealed record OrgScope
{
    public bool IsSuperUser { get; }

    public Guid? OrganizationId { get; }

    private OrgScope(bool isSuperUser, Guid? organizationId)
    {
        IsSuperUser = isSuperUser;
        OrganizationId = organizationId;
    }

    /// <summary>System-wide scope (the SuperUser role).</summary>
    public static OrgScope SuperUser { get; } = new(true, null);

    /// <summary>Subtree scope for an OrgAdmin of <paramref name="orgId"/>.</summary>
    public static OrgScope ForOrgAdmin(Guid orgId) => new(false, orgId);

    /// <summary>Fail-closed scope: not a SuperUser and no org — matches nothing.</summary>
    public static OrgScope None { get; } = new(false, null);
}
