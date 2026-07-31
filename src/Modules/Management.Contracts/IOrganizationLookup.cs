namespace LibreLms.Contracts.Management;

/// <summary>
/// Cross-module contract for looking up organization hierarchy information.
/// Implemented by the Management module and registered in DI.
/// </summary>
public interface IOrganizationLookup
{
    /// <summary>Get an organization by its ID.</summary>
    Task<OrganizationSummary?> GetOrganizationAsync(Guid orgId);

    /// <summary>
    /// Get all ancestor organization IDs including the org itself.
    /// </summary>
    Task<IList<Guid>> GetAncestorOrgIdsAsync(Guid orgId);
}
