using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Infrastructure;

namespace LibreLms.Modules.Management.Application;

/// <summary>
/// Implements the cross-module IOrganizationLookup contract.
/// Uses ManagementDbContext to resolve organization hierarchy queries.
/// </summary>
public class OrganizationLookup(ManagementDbContext context) : IOrganizationLookup
{
    public async Task<OrganizationSummary?> GetOrganizationAsync(Guid orgId)
    {
        var org = await context.Organizations
            .Where(o => o.Id == orgId && !o.IsDeleted)
            .Select(o => new OrganizationSummary(o.Id, o.Name, o.Description, o.ParentId))
            .FirstOrDefaultAsync();

        return org;
    }

    public async Task<IList<Guid>> GetAncestorOrgIdsAsync(Guid orgId)
    {
        var ancestors = new List<Guid>();
        var current = await context.Organizations.FindAsync(orgId);

        while (current is not null && !current.IsDeleted)
        {
            ancestors.Add(current.Id);
            if (!current.ParentId.HasValue)
                break;
            current = await context.Organizations
                .Where(o => o.Id == current.ParentId && !o.IsDeleted)
                .FirstOrDefaultAsync();
        }

        return ancestors;
    }
}
