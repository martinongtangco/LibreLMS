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
    /// <summary>
    /// Create a new user (learner or org admin). Admin-created accounts are verified.
    /// Throws <see cref="ForbiddenAccessException"/> when the target organization
    /// is outside the caller's scope (ADR 0010).
    /// </summary>
    public async Task<StudentProvisionedDto> CreateAsync(string name, string email, string password, string role, Guid organizationId, OrgScope scope)
    {
        // Validate role
        if (role is not RoleNames.Learner and not RoleNames.OrgAdmin and not RoleNames.SuperUser)
            throw new ArgumentException($"Invalid role: {role}. Must be SuperUser, OrgAdmin, or Learner.");

        if (!await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, organizationId))
            throw new ForbiddenAccessException("You cannot create users in an organization outside your scope.");

        return await provisioning.CreateAsync(name, email, password, role, organizationId, isVerified: true);
    }

    /// <summary>
    /// Get a user by ID with organization info.
    /// Throws <see cref="ForbiddenAccessException"/> when the user's organization
    /// is outside the caller's scope (ADR 0010).
    /// </summary>
    public async Task<UserDto?> GetByIdAsync(Guid id, OrgScope scope)
    {
        var student = await provisioning.GetByIdAsync(id);
        if (student is null)
            return null;

        if (!await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, student.OrganizationId))
            throw new ForbiddenAccessException("This user is outside your organization scope.");

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

    /// <summary>
    /// List users scoped to the given organization. The organization itself must
    /// be within the caller's scope (ADR 0010).
    /// </summary>
    public async Task<IList<UserDto>> ListByOrgScopeAsync(Guid orgId, string? roleFilter, OrgScope scope)
    {
        if (!await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, orgId))
            throw new ForbiddenAccessException("This organization is outside your scope.");

        var students = await provisioning.ListByOrgAsync(orgId, roleFilter);
        return await ToUserDtosAsync(students);
    }

    /// <summary>
    /// Update a user's details. The user's organization (and the new organization
    /// when changed) must be within the caller's scope (ADR 0010).
    /// </summary>
    public async Task<StudentProvisionedDto> UpdateAsync(Guid id, string? name, string? role, Guid? organizationId, OrgScope scope)
    {
        var existing = await provisioning.GetByIdAsync(id);
        if (existing is null)
            throw new KeyNotFoundException("User not found.");

        if (!await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, existing.OrganizationId))
            throw new ForbiddenAccessException("This user is outside your organization scope.");

        if (organizationId.HasValue &&
            organizationId.Value != existing.OrganizationId &&
            !await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, organizationId.Value))
            throw new ForbiddenAccessException("You cannot move users into an organization outside your scope.");

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

    /// <summary>
    /// Delete a user (cancels enrollments). The user's organization must be
    /// within the caller's scope (ADR 0010).
    /// </summary>
    public async Task DeleteAsync(Guid id, OrgScope scope)
    {
        var existing = await provisioning.GetByIdAsync(id);
        if (existing is null)
            throw new KeyNotFoundException("User not found.");

        if (!await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, existing.OrganizationId))
            throw new ForbiddenAccessException("This user is outside your organization scope.");

        // Prevent deleting the last SuperUser
        if (existing.Role == RoleNames.SuperUser)
        {
            var superUserCount = await userLookup.CountByRoleAsync(RoleNames.SuperUser);
            if (superUserCount <= 1)
                throw new InvalidOperationException("Cannot delete the last SuperUser.");
        }

        await provisioning.DeleteAsync(id);
    }

    /// <summary>
    /// List users. SuperUser sees everyone; an OrgAdmin sees only users whose
    /// organization is in their subtree (ADR 0010).
    /// </summary>
    public async Task<IList<UserDto>> ListAllAsync(string? roleFilter, OrgScope scope)
    {
        var students = await provisioning.ListAsync(roleFilter);
        var subtree = await OrgSubtree.GetSubtreeOrgIdsAsync(scope, orgLookup);
        if (subtree is not null)
            students = students.Where(s => subtree.Contains(s.OrganizationId)).ToList();
        return await ToUserDtosAsync(students);
    }

    /// <summary>
    /// Paged variant of ListAllAsync: delegates to IUserProvisioning.ListPagedAsync
    /// (the paged stored procedure filters to the scope's root org — SuperUser
    /// passes null for system-wide), then enriches org names for the page's
    /// distinct OrganizationIds.
    /// </summary>
    public async Task<UserPageResult> ListAllPagedAsync(string? search, string? roleFilter, int pageNumber, int pageSize, OrgScope scope)
    {
        var rootOrgId = scope.IsSuperUser ? null : scope.OrganizationId;
        var page = await provisioning.ListPagedAsync(search, roleFilter, pageNumber, pageSize, rootOrgId);
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
