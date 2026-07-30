using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Infrastructure;

namespace LibreLms.Modules.Management.Application;

/// <summary>
/// Service for managing organization hierarchy operations.
/// Handles CRUD, subtree traversal, and deletion safety checks.
/// </summary>
public class OrganizationService(ManagementDbContext context)
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
}
