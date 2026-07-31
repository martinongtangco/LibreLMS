namespace LibreLms.Host.ManagementAuth;

/// <summary>
/// Policy constant names for organization-scoped authorization.
/// Used with [Authorize(Policy = "SuperUserOnly")] etc.
/// </summary>
public static class OrgAuthPolicy
{
    /// <summary>Only SuperUsers may access this resource.</summary>
    public const string SuperUserOnly = "SuperUserOnly";

    /// <summary>SuperUsers and OrgAdmins may access this resource.</summary>
    public const string OrgAdminOrSuperUser = "OrgAdminOrSuperUser";

    /// <summary>Authenticated users with valid org scope may access this resource.</summary>
    public const string AuthenticatedWithOrgScope = "AuthenticatedWithOrgScope";
}
