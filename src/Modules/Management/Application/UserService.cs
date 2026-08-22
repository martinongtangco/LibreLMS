using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;
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

/// <summary>A page of user rows plus the filtered total count (spec 032).</summary>
public record UserPageResult(IList<UserDto> Items, int TotalCount);

/// <summary>
/// Service for managing users (learners and org admins) within organizational scope.
/// Spec 027 (R9): delegates all account work to the Enrollment module's IUserProvisioning
/// contract — this module no longer touches EnrollmentDbContext or Student directly.
/// Role validity and the last-SuperUser guards live here (Management policy).
/// </summary>
public class UserService(
    IUserProvisioning provisioning,
    IUserLookup userLookup,
    IOrganizationLookup orgLookup)
{
    /// <summary>Create a new user (learner or org admin). Admin-created accounts are verified.</summary>
    public async Task<StudentProvisionedDto> CreateAsync(string name, string email, string password, string role, Guid organizationId)
    {
        // Validate role
        if (role is not RoleNames.Learner and not RoleNames.OrgAdmin and not RoleNames.SuperUser)
            throw new ArgumentException($"Invalid role: {role}. Must be SuperUser, OrgAdmin, or Learner.");

        return await provisioning.CreateAsync(name, email, password, role, organizationId, isVerified: true);
    }

    /// <summary>Get a user by ID with organization info.</summary>
    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var student = await provisioning.GetByIdAsync(id);
        if (student is null)
            return null;

        var org = await orgLookup.GetOrganizationAsync(student.OrganizationId);

        return new UserDto(
            student.Id,
            student.Name,
            student.Email,
            student.Role,
            student.OrganizationId,
            org?.Name ?? "Unknown",
            student.CreatedAt
        );
    }

    /// <summary>List users scoped to the given organization.</summary>
    public async Task<IList<UserDto>> ListByOrgScopeAsync(Guid orgId, string? roleFilter = null)
    {
        var students = await provisioning.ListByOrgAsync(orgId, roleFilter);
        return await ToUserDtosAsync(students);
    }

    /// <summary>Update a user's details.</summary>
    public async Task<StudentProvisionedDto> UpdateAsync(Guid id, string? name, string? role, Guid? organizationId)
    {
        var existing = await provisioning.GetByIdAsync(id);
        if (existing is null)
            throw new KeyNotFoundException("User not found.");

        if (!string.IsNullOrEmpty(role))
        {
            // Prevent demoting the last SuperUser
            if (role != RoleNames.SuperUser && existing.Role == RoleNames.SuperUser)
            {
                var superUserCount = await userLookup.CountByRoleAsync(RoleNames.SuperUser);
                if (superUserCount <= 1)
                    throw new InvalidOperationException("Cannot demote the last SuperUser.");
            }

            if (role is not RoleNames.Learner and not RoleNames.OrgAdmin and not RoleNames.SuperUser)
                throw new ArgumentException($"Invalid role: {role}.");
        }

        return await provisioning.UpdateAsync(id, name, role, organizationId);
    }

    /// <summary>Delete a user (cancels enrollments).</summary>
    public async Task DeleteAsync(Guid id)
    {
        var existing = await provisioning.GetByIdAsync(id);
        if (existing is null)
            throw new KeyNotFoundException("User not found.");

        // Prevent deleting the last SuperUser
        if (existing.Role == RoleNames.SuperUser)
        {
            var superUserCount = await userLookup.CountByRoleAsync(RoleNames.SuperUser);
            if (superUserCount <= 1)
                throw new InvalidOperationException("Cannot delete the last SuperUser.");
        }

        await provisioning.DeleteAsync(id);
    }

    /// <summary>Get all users (SuperUser only).</summary>
    public async Task<IList<UserDto>> ListAllAsync(string? roleFilter = null)
    {
        var students = await provisioning.ListAsync(roleFilter);
        return await ToUserDtosAsync(students);
    }

    /// <summary>Paged variant of ListAllAsync: delegates to IUserProvisioning.ListPagedAsync,
    /// then enriches org names for the page's distinct OrganizationIds. Old method retained.</summary>
    public async Task<UserPageResult> ListAllPagedAsync(string? search, string? roleFilter, int pageNumber, int pageSize)
    {
        var page = await provisioning.ListPagedAsync(search, roleFilter, pageNumber, pageSize);
        var dtos = await ToUserDtosAsync(page.Items);
        return new UserPageResult(dtos, page.TotalCount);
    }

    private async Task<IList<UserDto>> ToUserDtosAsync(IList<StudentProvisionedDto> students)
    {
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

            dtos.Add(new UserDto(s.Id, s.Name, s.Email, s.Role, s.OrganizationId, orgName, s.CreatedAt));
        }

        return dtos;
    }
}
