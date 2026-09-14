using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibreLms.Tests.Management;

/// <summary>
/// Org-scope enforcement in OrganizationService (spec 052, ADR 0010).
/// Real OrganizationLookup over an InMemory ManagementDbContext so the EF
/// data and the lookup tree are one source of truth.
///
/// Org tree: Root → OrgA → OrgB; Root → OrgC (sibling of OrgA).
/// </summary>
public class OrganizationScopeTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid OrgC = Guid.NewGuid();

    private readonly ManagementDbContext _db;
    private readonly OrganizationService _service;

    private static OrgScope SuperUser => OrgScope.SuperUser;
    private static OrgScope AdminOfA => OrgScope.ForOrgAdmin(OrgA);

    public OrganizationScopeTests()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase($"OrgScopeTests_{Guid.NewGuid()}")
            .Options;
        _db = new ManagementDbContext(options);

        _db.Organizations.Add(new Organization { Id = Root, Name = "Root", ParentId = null });
        _db.Organizations.Add(new Organization { Id = OrgA, Name = "Org A", ParentId = Root });
        _db.Organizations.Add(new Organization { Id = OrgB, Name = "Org B", ParentId = OrgA });
        _db.Organizations.Add(new Organization { Id = OrgC, Name = "Org C", ParentId = Root });
        _db.SaveChanges();

        _service = new OrganizationService(
            _db,
            new FakeUserLookup(new FakeProvisioning()),
            new NoCoursesLookup(),
            new TreeLayoutService(),
            new OrganizationLookup(_db));
    }

    // ── ListAll ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAll_superuser_sees_every_org()
    {
        var orgs = await _service.ListAllAsync(SuperUser);
        var ids = orgs.Select(o => o.Id).ToHashSet();
        Assert.Contains(Root, ids);
        Assert.Contains(OrgA, ids);
        Assert.Contains(OrgB, ids);
        Assert.Contains(OrgC, ids);
    }

    [Fact]
    public async Task ListAll_orgadmin_sees_only_own_subtree()
    {
        var orgs = await _service.ListAllAsync(AdminOfA);
        var ids = orgs.Select(o => o.Id).ToHashSet();
        Assert.Contains(OrgA, ids);
        Assert.Contains(OrgB, ids);
        Assert.DoesNotContain(Root, ids);
        Assert.DoesNotContain(OrgC, ids);
    }

    // ── GetById / reads ────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.GetByIdAsync(OrgC, AdminOfA));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.GetByIdAsync(Root, AdminOfA));
    }

    [Fact]
    public async Task GetById_in_subtree_returns_org()
    {
        var org = await _service.GetByIdAsync(OrgB, AdminOfA);
        Assert.NotNull(org);
        Assert.Equal("Org B", org!.Name);
    }

    [Fact]
    public async Task GetById_missing_returns_null()
    {
        Assert.Null(await _service.GetByIdAsync(Guid.NewGuid(), SuperUser));
    }

    [Fact]
    public async Task GetByIdWithStatus_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.GetByIdWithStatusAsync(OrgC, AdminOfA));
    }

    [Fact]
    public async Task GetSubtreeAsync_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.GetSubtreeAsync(OrgC, AdminOfA));
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_under_foreign_parent_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.CreateAsync("New under C", null, OrgC, AdminOfA));
    }

    [Fact]
    public async Task Create_root_level_by_orgadmin_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.CreateAsync("Another root", null, null, AdminOfA));
    }

    [Fact]
    public async Task Create_under_subtree_child_succeeds()
    {
        var org = await _service.CreateAsync("New under B", null, OrgB, AdminOfA);
        Assert.Equal(OrgB, org.ParentId);
    }

    [Fact]
    public async Task Create_superuser_can_use_any_parent()
    {
        var org = await _service.CreateAsync("New under C", null, OrgC, SuperUser);
        Assert.Equal(OrgC, org.ParentId);
    }

    // ── Update / Delete / Disable / Enable ─────────────────────────────────

    [Fact]
    public async Task Update_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.UpdateAsync(OrgC, "Renamed", null, AdminOfA));
    }

    [Fact]
    public async Task Delete_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.DeleteAsync(OrgC, AdminOfA));
        // Still live (no side effects before the check).
        Assert.False(_db.Organizations.First(o => o.Id == OrgC).IsDeleted);
    }

    [Fact]
    public async Task CanDelete_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.CanDeleteAsync(OrgC, AdminOfA));
    }

    [Fact]
    public async Task Disable_enable_out_of_subtree_throw_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.DisableAsync(OrgC, AdminOfA));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.EnableAsync(OrgC, AdminOfA));
    }

    [Fact]
    public async Task Delete_in_subtree_succeeds()
    {
        await _service.DeleteAsync(OrgB, AdminOfA);
        Assert.True(_db.Organizations.First(o => o.Id == OrgB).IsDeleted);
    }

    // ── Chart ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Chart_orgadmin_is_pinned_to_own_subtree()
    {
        // Even if a caller passes a foreign root, an OrgAdmin's chart is pinned
        // to their own org (ADR 0010).
        var nodes = await _service.GetChartTreeAsync(Root, AdminOfA);
        var ids = nodes.Select(n => n.Id).ToHashSet();
        Assert.Contains(OrgA, ids);
        Assert.Contains(OrgB, ids);
        Assert.DoesNotContain(Root, ids);
        Assert.DoesNotContain(OrgC, ids);
    }

    [Fact]
    public async Task Chart_superuser_sees_everything()
    {
        var nodes = await _service.GetChartTreeAsync(null, SuperUser);
        var ids = nodes.Select(n => n.Id).ToHashSet();
        Assert.Contains(Root, ids);
        Assert.Contains(OrgC, ids);
    }
}

/// <summary>ICourseLookup fake — no courses by default; seedable for visibility tests.</summary>
internal sealed class NoCoursesLookup : ICourseLookup
{
    public List<CourseSummary> Courses { get; } = [];

    public Task<CourseSummary?> GetCourseAsync(Guid courseId)
        => Task.FromResult(Courses.FirstOrDefault(c => c.Id == courseId));
    public Task<int> CountAsync() => Task.FromResult(Courses.Count);
    public Task<int> CountByOrgAsync(Guid organizationId)
        => Task.FromResult(Courses.Count(c => c.OrganizationId == organizationId));
    public Task<IReadOnlyDictionary<Guid, int>> GetCourseCountsByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var ids = organizationIds.ToHashSet();
        var dict = Courses.Where(c => ids.Contains(c.OrganizationId))
            .GroupBy(c => c.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(dict);
    }
    public Task<IList<string>> GetDistinctCategoriesAsync()
        => Task.FromResult<IList<string>>(Courses.Select(c => c.Category).Distinct().OrderBy(c => c).ToList());
    public Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds)
    {
        var ids = courseIds.ToHashSet();
        return Task.FromResult<IList<CourseSummary>>(Courses.Where(c => ids.Contains(c.Id)).ToList());
    }
    public Task<IList<CourseSummary>> ListByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var ids = organizationIds.ToHashSet();
        return Task.FromResult<IList<CourseSummary>>(Courses.Where(c => ids.Contains(c.OrganizationId)).ToList());
    }
    public Task<IList<CourseSummary>> ListAllAsync() => Task.FromResult<IList<CourseSummary>>(Courses.ToList());
}
