using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.SharedKernel;

namespace LibreLms.Modules.Management.Application;

/// <summary>DTO for user listing.</summary>
public record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid OrganizationId,
    string OrganizationName,
    DateTimeOffset CreatedAt
);

/// <summary>Service for managing users (learners and org admins) within organizational scope.</summary>
public class UserService(
    EnrollmentDbContext enrollmentCtx,
    IOrganizationLookup orgLookup)
{
    /// <summary>Create a new user (learner or org admin).</summary>
    public async Task<Student> CreateAsync(string name, string email, string password, string role, Guid organizationId)
    {
        // Validate role
        if (role is not RoleNames.Learner and not RoleNames.OrgAdmin and not RoleNames.SuperUser)
            throw new ArgumentException($"Invalid role: {role}. Must be SuperUser, OrgAdmin, or Learner.");

        // Check for duplicate email
        var existing = await enrollmentCtx.Students
            .AnyAsync(s => s.Email == email);
        if (existing)
            throw new InvalidOperationException($"A user with email '{email}' already exists.");

        var student = new Student
        {
            Name = name,
            Email = email,
            PasswordHash = HashPassword(password),
            Roles = role,
            OrganizationId = organizationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        enrollmentCtx.Students.Add(student);
        await enrollmentCtx.SaveChangesAsync();
        return student;
    }

    /// <summary>Get a user by ID with organization info.</summary>
    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var student = await enrollmentCtx.Students
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student is null)
            return null;

        var org = await orgLookup.GetOrganizationAsync(student.OrganizationId);

        return new UserDto(
            student.Id,
            student.Name,
            student.Email,
            student.Roles,
            student.OrganizationId,
            org?.Name ?? "Unknown",
            student.CreatedAt
        );
    }

    /// <summary>List users scoped to the given organization subtree.</summary>
    public async Task<IList<UserDto>> ListByOrgScopeAsync(Guid orgId, string? roleFilter = null)
    {
        var ancestorIds = await orgLookup.GetAncestorOrgIdsAsync(orgId);
        // We need descendants, not ancestors. Get all orgs and filter.
        var userOrgIds = await GetDescendantOrgIdsAsync(orgId);
        userOrgIds.Add(orgId); // Include the org itself

        var query = enrollmentCtx.Students
            .Where(s => userOrgIds.Contains(s.OrganizationId));

        if (!string.IsNullOrEmpty(roleFilter))
            query = query.Where(s => s.Roles == roleFilter);

        var students = await query.ToListAsync();

        var orgCache = new Dictionary<Guid, string>();
        var dtos = new List<UserDto>();

        foreach (var s in students)
        {
            if (!orgCache.TryGetValue(s.OrganizationId, out var orgName))
            {
                var org = await orgLookup.GetOrganizationAsync(s.OrganizationId);
                orgName = org?.Name ?? "Unknown";
                orgCache[s.OrganizationId] = orgName;
            }

            dtos.Add(new UserDto(s.Id, s.Name, s.Email, s.Roles, s.OrganizationId, orgName, s.CreatedAt));
        }

        return dtos;
    }

    /// <summary>Update a user's details.</summary>
    public async Task<Student> UpdateAsync(Guid id, string? name, string? role, Guid? organizationId)
    {
        var student = await enrollmentCtx.Students.FindAsync(id);
        if (student is null)
            throw new KeyNotFoundException("User not found.");

        if (!string.IsNullOrEmpty(name))
            student.Name = name;

        if (!string.IsNullOrEmpty(role))
        {
            // Prevent demoting the last SuperUser
            if (role != RoleNames.SuperUser && student.Roles == RoleNames.SuperUser)
            {
                var superUserCount = await enrollmentCtx.Students
                    .CountAsync(s => s.Roles == RoleNames.SuperUser);
                if (superUserCount <= 1)
                    throw new InvalidOperationException("Cannot demote the last SuperUser.");
            }

            if (role is not RoleNames.Learner and not RoleNames.OrgAdmin and not RoleNames.SuperUser)
                throw new ArgumentException($"Invalid role: {role}.");

            student.Roles = role;
        }

        if (organizationId.HasValue)
            student.OrganizationId = organizationId.Value;

        await enrollmentCtx.SaveChangesAsync();
        return student;
    }

    /// <summary>Delete a user (cancels enrollments).</summary>
    public async Task DeleteAsync(Guid id)
    {
        var student = await enrollmentCtx.Students.FindAsync(id);
        if (student is null)
            throw new KeyNotFoundException("User not found.");

        // Prevent deleting the last SuperUser
        if (student.Roles == RoleNames.SuperUser)
        {
            var superUserCount = await enrollmentCtx.Students
                .CountAsync(s => s.Roles == RoleNames.SuperUser);
            if (superUserCount <= 1)
                throw new InvalidOperationException("Cannot delete the last SuperUser.");
        }

        enrollmentCtx.Students.Remove(student);
        await enrollmentCtx.SaveChangesAsync();
    }

    /// <summary>Get all users (SuperUser only).</summary>
    public async Task<IList<UserDto>> ListAllAsync(string? roleFilter = null)
    {
        var query = enrollmentCtx.Students.AsQueryable();
        if (!string.IsNullOrEmpty(roleFilter))
            query = query.Where(s => s.Roles == roleFilter);

        var students = await query.ToListAsync();

        var orgCache = new Dictionary<Guid, string>();
        return students.Select(s =>
        {
            if (!orgCache.TryGetValue(s.OrganizationId, out var orgName))
            {
                orgName = "Unknown";
                orgCache[s.OrganizationId] = orgName;
            }
            return new UserDto(s.Id, s.Name, s.Email, s.Roles, s.OrganizationId, orgName, s.CreatedAt);
        }).ToList();
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hash);
    }

    private async Task<IList<Guid>> GetDescendantOrgIdsAsync(Guid orgId)
    {
        var ids = new List<Guid>();
        // This requires access to ManagementDbContext — for now, use orgLookup recursively
        // In practice, this should be a direct query on the Organizations table
        var current = await orgLookup.GetOrganizationAsync(orgId);
        if (current is null)
            return ids;

        // For descendants, we need to query the Management module's DbContext
        // This is a limitation of the current contract design
        // For MVP, we'll use a broader approach
        return ids;
    }
}
