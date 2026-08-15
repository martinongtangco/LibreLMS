using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Endpoints;
using LibreLms.Modules.Management.Infrastructure;

namespace LibreLms.Modules.Management.Application;

/// <summary>
/// Service for managing organization hierarchy operations.
/// Handles CRUD, subtree traversal, deletion safety checks, and chart data generation.
/// </summary>
/// <summary>Spec 027 (R9): user/course counts come from contracts (IUserLookup,
/// ICourseLookup) — only Management-owned data comes from ManagementDbContext.</summary>
public class OrganizationService(
    ManagementDbContext context,
    IUserLookup userLookup,
    ICourseLookup courseLookup,
    TreeLayoutService layoutService)
{
    /// <summary>Create a new organization.</summary>
    public async Task<Organization> CreateAsync(string name, string? description, Guid? parentId)
    {
        // Check for duplicate name within parent
        var existing = await context.Organizations
            .AnyAsync(o => o.Name == name && o.ParentId == parentId && !o.IsDeleted);

        if (existing)
            throw new InvalidOperationException($"An organization with name '{name}' already exists under this parent.");

        // Enforce single root
        if (!parentId.HasValue && await context.Organizations.AnyAsync(o => o.ParentId == null && !o.IsDeleted))
            throw new InvalidOperationException("A root organization already exists.");

        var org = new Organization
        {
            Name = name,
            Description = description,
            ParentId = parentId
        };

        context.Organizations.Add(org);
        await context.SaveChangesAsync();
        return org;
    }

    /// <summary>Get an organization by ID.</summary>
    public async Task<Organization?> GetByIdAsync(Guid id)
    {
        return await context.Organizations
            .Include(o => o.Children)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
    }

    /// <summary>Get all organizations under a specific parent (direct children only).</summary>
    public async Task<IList<Organization>> ListByParentAsync(Guid? parentId)
    {
        return await context.Organizations
            .Where(o => o.ParentId == parentId && !o.IsDeleted)
            .OrderBy(o => o.Name)
            .ToListAsync();
    }

    /// <summary>Get the entire subtree (all descendants) for an organization.</summary>
    public async Task<IList<Organization>> GetSubtreeAsync(Guid orgId)
    {
        var all = new List<Organization>();
        var queue = new Queue<Organization>();

        var root = await context.Organizations.FindAsync(orgId);
        if (root is null || root.IsDeleted)
            return all;

        queue.Enqueue(root);
        all.Add(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await context.Organizations
                .Where(o => o.ParentId == current.Id && !o.IsDeleted)
                .ToListAsync();

            foreach (var child in children)
            {
                all.Add(child);
                queue.Enqueue(child);
            }
        }

        return all;
    }

    /// <summary>Update organization name and description.</summary>
    public async Task<Organization> UpdateAsync(Guid id, string name, string? description)
    {
        var org = await context.Organizations.FindAsync(id);
        if (org is null || org.IsDeleted)
            throw new KeyNotFoundException("Organization not found.");

        // Check for duplicate name (excluding self)
        var duplicate = await context.Organizations
            .AnyAsync(o => o.Name == name && o.ParentId == org.ParentId && o.Id != id && !o.IsDeleted);

        if (duplicate)
            throw new InvalidOperationException($"An organization with name '{name}' already exists under this parent.");

        org.Name = name;
        org.Description = description;
        await context.SaveChangesAsync();
        return org;
    }

    /// <summary>Soft-delete an organization.</summary>
    public async Task DeleteAsync(Guid id)
    {
        var org = await context.Organizations
            .Include(o => o.Children)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (org is null || org.IsDeleted)
            throw new KeyNotFoundException("Organization not found.");

        // Prevent deleting root
        if (!org.ParentId.HasValue)
            throw new InvalidOperationException("Cannot delete the root organization.");

        org.IsDeleted = true;
        await context.SaveChangesAsync();
    }

    /// <summary>Check if an organization can be safely deleted (no dependents).</summary>
    public async Task<(bool CanDelete, string? Reason)> CanDeleteAsync(Guid id)
    {
        var org = await context.Organizations
            .Include(o => o.Children)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (org is null || org.IsDeleted)
            return (false, "Organization not found.");

        if (!org.ParentId.HasValue)
            return (false, "Cannot delete the root organization.");

        var children = org.Children.ToList();
        if (children.Count > 0)
            return (false, $"Organization has {children.Count} child organization(s). Remove or reassign them first.");

        return (true, null);
    }

    /// <summary>Get all active (non-deleted) organizations.</summary>
    public async Task<IList<Organization>> ListAllAsync()
    {
        return await context.Organizations
            .Where(o => !o.IsDeleted)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get the complete organization chart data with layout positions and summary counts.
    /// If rootOrgId is provided, only returns that subtree (for OrgAdmin scoping).
    /// </summary>
    public async Task<IList<OrgChartNodeDto>> GetChartTreeAsync(Guid? rootOrgId = null)
    {
        // Fetch organizations (all or scoped subtree)
        var orgs = rootOrgId.HasValue
            ? await FetchSubtreeAsync(rootOrgId.Value)
            : await context.Organizations
                .Where(o => !o.IsDeleted)
                .Include(o => o.Children)
                .ToListAsync();

        if (orgs.Count == 0)
            return Array.Empty<OrgChartNodeDto>();

        // Build a flat list with children populated for layout
        var orgMap = orgs.ToDictionary(o => o.Id);
        foreach (var org in orgs)
        {
            org.Children = org.Children.Where(c => orgMap.ContainsKey(c.Id)).ToList();
        }

        // Compute layout positions using the tree layout algorithm
        var layoutResults = layoutService.ComputeLayout(orgs);

        // Compute user and course counts per organization
        var orgIds = orgs.Select(o => o.Id).ToList();

        // User counts per org (cross-module contract), filtered to the charted orgs
        var allUserCounts = await userLookup.GetLearnerCountsByOrgAsync();
        var userCounts = allUserCounts
            .Where(c => orgIds.Contains(c.OrganizationId))
            .ToDictionary(c => c.OrganizationId, c => c.Count);

        // Course counts per org (cross-module contract; dev scale: one count per org)
        var courseCounts = new Dictionary<Guid, int>();
        foreach (var orgId in orgIds)
        {
            courseCounts[orgId] = await courseLookup.CountByOrgAsync(orgId);
        }

        // Build DTOs from layout results
        return layoutResults
            .Select(lr =>
            {
                userCounts.TryGetValue(lr.Org.Id, out var userCount);
                courseCounts.TryGetValue(lr.Org.Id, out var courseCount);

                return new OrgChartNodeDto(
                    Id: lr.Org.Id,
                    Name: lr.Org.Name,
                    Description: lr.Org.Description,
                    Depth: lr.Depth,
                    X: lr.X,
                    Y: lr.Y,
                    IsDisabled: lr.Org.IsDisabled,
                    IsRoot: !lr.Org.ParentId.HasValue,
                    UserCount: userCount,
                    CourseCount: courseCount,
                    HasChildren: lr.Org.Children.Any(),
                    ParentId: lr.Org.ParentId
                );
            })
            .ToList();
    }

    /// <summary>
    /// Disable an organization and all its descendants.
    /// Root organizations cannot be disabled.
    /// </summary>
    public async Task DisableAsync(Guid id)
    {
        var org = await context.Organizations.FindAsync(id);
        if (org is null || org.IsDeleted)
            throw new KeyNotFoundException("Organization not found.");

        // Prevent disabling root
        if (!org.ParentId.HasValue)
            throw new InvalidOperationException("Cannot disable the root organization.");

        // Cascade to all descendants
        var descendants = await GetDescendantIdsAsync(id);
        descendants.Add(id);

        foreach (var descId in descendants)
        {
            var desc = await context.Organizations.FindAsync(descId);
            if (desc is not null && !desc.IsDeleted)
                desc.IsDisabled = true;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Enable an organization and all its descendants.
    /// </summary>
    public async Task EnableAsync(Guid id)
    {
        var org = await context.Organizations.FindAsync(id);
        if (org is null || org.IsDeleted)
            throw new KeyNotFoundException("Organization not found.");

        // Cascade to all descendants
        var descendants = await GetDescendantIdsAsync(id);
        descendants.Add(id);

        foreach (var descId in descendants)
        {
            var desc = await context.Organizations.FindAsync(descId);
            if (desc is not null && !desc.IsDeleted)
                desc.IsDisabled = false;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Get an organization with its current user and course counts.
    /// Used by the edit dialog to show summary data.
    /// </summary>
    public async Task<(Organization Org, int UserCount, int CourseCount)> GetByIdWithStatusAsync(Guid id)
    {
        var org = await context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

        if (org is null)
            throw new KeyNotFoundException("Organization not found.");

        var userCount = await userLookup.CountLearnersAsync(id);

        var courseCount = await courseLookup.CountByOrgAsync(id);

        return (org, userCount, courseCount);
    }

    private async Task<IList<Organization>> FetchSubtreeAsync(Guid rootId)
    {
        var all = new List<Organization>();
        var queue = new Queue<Organization>();

        var root = await context.Organizations
            .Include(o => o.Children)
            .FirstOrDefaultAsync(o => o.Id == rootId && !o.IsDeleted);

        if (root is null)
            return all;

        queue.Enqueue(root);
        all.Add(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await context.Organizations
                .Where(o => o.ParentId == current.Id && !o.IsDeleted)
                .ToListAsync();

            current.Children = children;
            foreach (var child in children)
            {
                all.Add(child);
                queue.Enqueue(child);
            }
        }

        return all;
    }

    private async Task<HashSet<Guid>> GetDescendantIdsAsync(Guid orgId)
    {
        var ids = new HashSet<Guid>();
        var queue = new Queue<Guid>(new[] { orgId });

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var children = await context.Organizations
                .Where(o => o.ParentId == currentId && !o.IsDeleted)
                .Select(o => o.Id)
                .ToListAsync();

            foreach (var childId in children)
            {
                ids.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return ids;
    }
}
