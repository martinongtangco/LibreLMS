using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Application;
using LibreLms.SharedKernel;
using UserScopeInfo = LibreLms.Contracts.Enrollment.UserScopeInfo;

namespace LibreLms.Tests.Management;

/// <summary>
/// Org-scope enforcement in UserService (spec 052, ADR 0010).
///
/// Org tree used throughout:
///   Root → OrgA → OrgB
///   Root → OrgC            (sibling of OrgA)
///
/// An OrgAdmin of OrgA's scope covers {OrgA, OrgB} and nothing else.
/// </summary>
public class UserServiceScopeTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid OrgC = Guid.NewGuid();

    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();
    private static readonly Guid UserC = Guid.NewGuid();
    private static readonly Guid UserRoot = Guid.NewGuid();

    private readonly FakeProvisioning _provisioning = new();
    private readonly FakeOrgLookup _orgLookup = new();
    private readonly UserService _service;

    private static OrgScope SuperUser => OrgScope.SuperUser;
    private static OrgScope OrgAdminOfA => OrgScope.ForOrgAdmin(OrgA);
    private static OrgScope Nobody => OrgScope.None;

    public UserServiceScopeTests()
    {
        Seed();
        _service = new UserService(_provisioning, new FakeUserLookup(_provisioning), _orgLookup);
    }

    private void Seed()
    {
        _orgLookup.AddOrg(Root, "Root", parentId: null);
        _orgLookup.AddOrg(OrgA, "Org A", parentId: Root);
        _orgLookup.AddOrg(OrgB, "Org B", parentId: OrgA);
        _orgLookup.AddOrg(OrgC, "Org C", parentId: Root);

        _provisioning.Add(new StudentProvisionedDto(UserA, "User A", "a@example.com", RoleNames.Learner, OrgA, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(UserB, "User B", "b@example.com", RoleNames.Learner, OrgB, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(UserC, "User C", "c@example.com", RoleNames.Learner, OrgC, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(UserRoot, "User Root", "root@example.com", RoleNames.Learner, Root, DateTimeOffset.UtcNow, true));
    }

    // ── ListAll ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAll_superuser_sees_every_org()
    {
        var users = await _service.ListAllAsync(null, SuperUser);
        var emails = users.Select(u => u.Email).ToList();

        Assert.Contains("a@example.com", emails);
        Assert.Contains("b@example.com", emails);
        Assert.Contains("c@example.com", emails);
        Assert.Contains("root@example.com", emails);
    }

    [Fact]
    public async Task ListAll_orgadmin_sees_only_own_subtree()
    {
        var users = await _service.ListAllAsync(null, OrgAdminOfA);
        var emails = users.Select(u => u.Email).ToList();

        Assert.Contains("a@example.com", emails);   // own org
        Assert.Contains("b@example.com", emails);   // descendant org
        Assert.DoesNotContain("c@example.com", emails);    // sibling subtree
        Assert.DoesNotContain("root@example.com", emails); // ancestor org
    }

    [Fact]
    public async Task ListAll_fail_closed_scope_sees_nothing()
    {
        var users = await _service.ListAllAsync(null, Nobody);
        Assert.Empty(users);
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_out_of_subtree_throws_forbidden()
    {
        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.GetByIdAsync(UserC, OrgAdminOfA));
        Assert.Contains("outside your organization scope", ex.Message);
    }

    [Fact]
    public async Task GetById_in_subtree_returns_user()
    {
        var user = await _service.GetByIdAsync(UserB, OrgAdminOfA);
        Assert.NotNull(user);
        Assert.Equal("b@example.com", user!.Email);
    }

    [Fact]
    public async Task GetById_missing_returns_null()
    {
        var user = await _service.GetByIdAsync(Guid.NewGuid(), SuperUser);
        Assert.Null(user);
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_in_foreign_org_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.CreateAsync("New C", "newc@example.com", "Subtree@Org#2026", RoleNames.Learner, OrgC, OrgAdminOfA));
    }

    [Fact]
    public async Task Create_in_subtree_child_org_succeeds()
    {
        var created = await _service.CreateAsync("New B", "newb@example.com", "Subtree@Org#2026", RoleNames.Learner, OrgB, OrgAdminOfA);
        Assert.Equal(OrgB, created.OrganizationId);
    }

    [Fact]
    public async Task Create_superuser_can_target_any_org()
    {
        var created = await _service.CreateAsync("New C", "newc2@example.com", "Subtree@Org#2026", RoleNames.Learner, OrgC, SuperUser);
        Assert.Equal(OrgC, created.OrganizationId);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.UpdateAsync(UserC, "Renamed C", null, null, OrgAdminOfA));
    }

    [Fact]
    public async Task Update_move_into_out_of_subtree_org_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.UpdateAsync(UserA, null, null, OrgC, OrgAdminOfA));
    }

    [Fact]
    public async Task Update_in_subtree_succeeds()
    {
        var updated = await _service.UpdateAsync(UserA, "Renamed A", null, null, OrgAdminOfA);
        Assert.Equal("Renamed A", updated.Name);
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.DeleteAsync(UserC, OrgAdminOfA));

        // The user must still exist (no side effects before the check).
        Assert.NotNull(await _provisioning.GetByIdAsync(UserC));
    }

    [Fact]
    public async Task Delete_in_subtree_succeeds()
    {
        await _service.DeleteAsync(UserB, OrgAdminOfA);
        Assert.Null(await _provisioning.GetByIdAsync(UserB));
    }

    // ── ListByOrgScope ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListByOrgScope_foreign_org_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.ListByOrgScopeAsync(OrgC, null, OrgAdminOfA));
    }

    [Fact]
    public async Task ListByOrgScope_in_subtree_returns_users()
    {
        var users = await _service.ListByOrgScopeAsync(OrgB, null, OrgAdminOfA);
        Assert.Single(users);
        Assert.Equal("b@example.com", users[0].Email);
    }
}

/// <summary>In-memory IUserProvisioning fake.</summary>
internal sealed class FakeProvisioning : IUserProvisioning
{
    private readonly List<StudentProvisionedDto> _students = [];

    public void Add(StudentProvisionedDto dto) => _students.Add(dto);

    public Task<StudentProvisionedDto> CreateAsync(string name, string email, string password,
        string role, Guid organizationId, bool isVerified)
    {
        var dto = new StudentProvisionedDto(Guid.NewGuid(), name, email.ToLowerInvariant(), role,
            organizationId, DateTimeOffset.UtcNow, isVerified);
        _students.Add(dto);
        return Task.FromResult(dto);
    }

    public Task<StudentProvisionedDto?> GetByIdAsync(Guid studentId)
        => Task.FromResult(_students.FirstOrDefault(s => s.Id == studentId));

    public Task<IList<StudentProvisionedDto>> ListByOrgAsync(Guid orgId, string? roleFilter = null)
        => Task.FromResult<IList<StudentProvisionedDto>>(
            _students.Where(s => s.OrganizationId == orgId && (roleFilter is null || s.Role == roleFilter)).ToList());

    public Task<IList<StudentProvisionedDto>> ListAsync(string? roleFilter = null)
        => Task.FromResult<IList<StudentProvisionedDto>>(
            _students.Where(s => roleFilter is null || s.Role == roleFilter).ToList());

    public Task<StudentPageResult> ListPagedAsync(string? search, string? roleFilter, int pageNumber, int pageSize, Guid? rootOrgId = null)
    {
        // Unit-level fake: ignore paging/root filtering (SP-level behavior is
        // covered by Enrollment.Tests + E2E).
        var all = _students.Where(s => roleFilter is null || s.Role == roleFilter).ToList();
        return Task.FromResult(new StudentPageResult(all, all.Count));
    }

    public Task<StudentProvisionedDto> UpdateAsync(Guid studentId, string? name, string? role, Guid? organizationId, string? avatarPath = null)
    {
        var existing = _students.First(s => s.Id == studentId);
        var updated = existing with
        {
            Name = name ?? existing.Name,
            Role = role ?? existing.Role,
            OrganizationId = organizationId ?? existing.OrganizationId
        };
        _students[_students.IndexOf(existing)] = updated;
        return Task.FromResult(updated);
    }

    public Task DeleteAsync(Guid studentId)
    {
        _students.RemoveAll(s => s.Id == studentId);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByEmailAsync(string email)
        => Task.FromResult(_students.Any(s => s.Email == email?.ToLowerInvariant()));
}

/// <summary>In-memory IOrganizationLookup fake with a parent map.</summary>
internal sealed class FakeOrgLookup : IOrganizationLookup
{
    private sealed record OrgEntry(string Name, Guid? ParentId);
    private readonly Dictionary<Guid, OrgEntry> _orgs = new();

    public void AddOrg(Guid id, string name, Guid? parentId) => _orgs[id] = new OrgEntry(name, parentId);

    public Task<OrganizationSummary?> GetOrganizationAsync(Guid orgId)
        => Task.FromResult(_orgs.TryGetValue(orgId, out var o) ? new OrganizationSummary(orgId, o.Name, null, o.ParentId) : null);

    public Task<IList<Guid>> GetAncestorOrgIdsAsync(Guid orgId)
    {
        var ancestors = new List<Guid>();
        var current = _orgs.TryGetValue(orgId, out var o) ? orgId : (Guid?)null;
        while (current.HasValue)
        {
            ancestors.Add(current.Value);
            current = _orgs[current.Value].ParentId;
        }
        return Task.FromResult<IList<Guid>>(ancestors);
    }

    public Task<IList<Guid>> GetChildOrgIdsAsync(Guid parentId)
        => Task.FromResult<IList<Guid>>(_orgs.Where(kv => kv.Value.ParentId == parentId).Select(kv => kv.Key).ToList());
}

/// <summary>IUserLookup fake: minimal read-side facts; role count is ample so the
/// last-SuperUser guards never trip in these tests.</summary>
internal sealed class FakeUserLookup(FakeProvisioning provisioning) : IUserLookup
{
    public Task<UserScopeInfo?> GetUserScopeAsync(Guid studentId)
    {
        var s = provisioning.GetByIdAsync(studentId).GetAwaiter().GetResult();
        return Task.FromResult(s is null ? null : new UserScopeInfo(s.OrganizationId, s.Role));
    }

    public Task<int> CountLearnersAsync(Guid? organizationId = null)
        => Task.FromResult(10);

    public Task<IList<OrgLearnerCount>> GetLearnerCountsByOrgAsync()
        => Task.FromResult<IList<OrgLearnerCount>>([]);

    public Task<string?> GetUserNameAsync(Guid studentId)
    {
        var s = provisioning.GetByIdAsync(studentId).GetAwaiter().GetResult();
        return Task.FromResult<string?>(s?.Name);
    }

    public Task<IList<UserSummary>> GetUsersAsync(IEnumerable<Guid> studentIds)
    {
        var ids = studentIds.ToHashSet();
        var list = provisioning.ListAsync(null).GetAwaiter().GetResult()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new UserSummary(s.Id, s.Name, s.Email, s.OrganizationId))
            .ToList();
        return Task.FromResult<IList<UserSummary>>(list);
    }

    public Task<int> CountByRoleAsync(string role)
        => Task.FromResult(10);
}
