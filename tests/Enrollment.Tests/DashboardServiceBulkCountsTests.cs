using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Enrollment.Tests;

/// <summary>
/// Spec 048 (E7) — counting-fake test at the CALLER level:
/// <c>DashboardService.GetOrgMetricsAsync</c> must compute the subtree course and
/// enrollment totals with exactly ONE <c>ICourseLookup.GetCourseCountsByOrgsAsync</c>
/// and ONE <c>IEnrollmentAdmin.CountEnrollmentsByOrgsAsync</c> call — no per-org
/// <c>CountByOrgAsync</c>/<c>CountEnrollmentsAsync</c> fan-out. ManagementDbContext
/// is EF InMemory (same pattern as the other Enrollment.Tests).
/// </summary>
public class DashboardServiceBulkCountsTests : IDisposable
{
    private readonly ManagementDbContext _managementCtx;
    private readonly BulkCountingCourseLookup _courseLookup;
    private readonly BulkCountingEnrollmentAdmin _enrollmentAdmin;
    private readonly DashboardService _service;

    private readonly Guid _rootOrg = Guid.NewGuid();
    private readonly Guid _childA = Guid.NewGuid();
    private readonly Guid _childB = Guid.NewGuid();
    private readonly Guid _outsideOrg = Guid.NewGuid();

    public DashboardServiceBulkCountsTests()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase(databaseName: $"DashboardBulkTests_{Guid.NewGuid()}")
            .Options;

        _managementCtx = new ManagementDbContext(options);
        _courseLookup = new BulkCountingCourseLookup();
        _enrollmentAdmin = new BulkCountingEnrollmentAdmin();
        _service = new DashboardService(_managementCtx, new StubUserLookup(), _enrollmentAdmin, _courseLookup);

        _managementCtx.Organizations.AddRange(
            new Organization { Id = _rootOrg, Name = "Root", ParentId = null },
            new Organization { Id = _childA, Name = "Child A", ParentId = _rootOrg },
            new Organization { Id = _childB, Name = "Child B", ParentId = _rootOrg },
            new Organization { Id = _outsideOrg, Name = "Outside", ParentId = null });
        _managementCtx.SaveChanges();

        // Fake facts: per-org course counts and per-org enrollment counts.
        _courseLookup.SetCount(_rootOrg, 2);
        _courseLookup.SetCount(_childA, 3);
        _courseLookup.SetCount(_childB, 0); // zero-course org: absent from bulk dict
        _courseLookup.SetCount(_outsideOrg, 99);
        _enrollmentAdmin.SetCount(_rootOrg, 5);
        _enrollmentAdmin.SetCount(_childA, 7);
        _enrollmentAdmin.SetCount(_childB, 1);
        _enrollmentAdmin.SetCount(_outsideOrg, 88);
    }

    public void Dispose() => _managementCtx.Dispose();

    [Fact]
    public async Task GetOrgMetricsAsync_UsesOneBulkCallPerContract_InsteadOfPerOrgLoop()
    {
        var metrics = await _service.GetOrgMetricsAsync(_rootOrg);

        // Exactly one bulk call per contract — no per-org fan-out.
        Assert.Equal(1, _courseLookup.GetCourseCountsByOrgsCalls);
        Assert.Equal(0, _courseLookup.CountByOrgCalls);
        Assert.Equal(1, _enrollmentAdmin.CountEnrollmentsByOrgsCalls);
        Assert.Equal(0, _enrollmentAdmin.PerOrgCountCalls);

        // The bulk calls must cover the org itself + every descendant, and none other.
        var expectedOrgs = new[] { _rootOrg, _childA, _childB }.ToHashSet();
        Assert.Equal(expectedOrgs, _courseLookup.LastBulkOrgIds!.ToHashSet());
        Assert.Equal(expectedOrgs, _enrollmentAdmin.LastBulkOrgIds!.ToHashSet());

        // Totals equal the sum of the per-org facts (child B contributes 0 courses).
        Assert.Equal(5, metrics.CourseCount);
        Assert.Equal(13, metrics.EnrollmentCount);
        Assert.Equal("Root", metrics.OrganizationName);
    }
}

/// <summary>ICourseLookup fake that counts per-org vs. bulk count calls (spec 048).</summary>
public class BulkCountingCourseLookup : ICourseLookup
{
    private readonly Dictionary<Guid, int> _counts = new();

    public int CountByOrgCalls { get; private set; }
    public int GetCourseCountsByOrgsCalls { get; private set; }
    public List<Guid>? LastBulkOrgIds { get; private set; }

    public void SetCount(Guid orgId, int count) => _counts[orgId] = count;

    public Task<int> CountByOrgAsync(Guid organizationId)
    {
        CountByOrgCalls++;
        _counts.TryGetValue(organizationId, out var count);
        return Task.FromResult(count);
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetCourseCountsByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        GetCourseCountsByOrgsCalls++;
        var orgs = organizationIds.Distinct().ToList();
        LastBulkOrgIds = orgs;
        var dict = orgs
            .Where(id => _counts.TryGetValue(id, out var c) && c > 0)
            .ToDictionary(id => id, id => _counts[id]);
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(dict);
    }

    // Unused by the tests — contract completeness.
    public Task<CourseSummary?> GetCourseAsync(Guid courseId) => Task.FromResult<CourseSummary?>(null);
    public Task<int> CountAsync() => Task.FromResult(0);
    public Task<IList<string>> GetDistinctCategoriesAsync() => Task.FromResult<IList<string>>(Array.Empty<string>());
    public Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds)
        => Task.FromResult<IList<CourseSummary>>(Array.Empty<CourseSummary>());
    public Task<IList<CourseSummary>> ListByOrgsAsync(IEnumerable<Guid> organizationIds)
        => Task.FromResult<IList<CourseSummary>>(Array.Empty<CourseSummary>());
    public Task<IList<CourseSummary>> ListAllAsync()
        => Task.FromResult<IList<CourseSummary>>(Array.Empty<CourseSummary>());
}

/// <summary>IEnrollmentAdmin fake that counts per-org vs. bulk count calls (spec 048).</summary>
public class BulkCountingEnrollmentAdmin : IEnrollmentAdmin
{
    private readonly Dictionary<Guid, int> _counts = new();

    public int PerOrgCountCalls { get; private set; }
    public int CountEnrollmentsByOrgsCalls { get; private set; }
    public List<Guid>? LastBulkOrgIds { get; private set; }

    public void SetCount(Guid orgId, int count) => _counts[orgId] = count;

    public Task<int> CountEnrollmentsAsync(Guid? organizationId = null)
    {
        if (organizationId is not null)
        {
            PerOrgCountCalls++;
            _counts.TryGetValue(organizationId.Value, out var count);
            return Task.FromResult(count);
        }
        return Task.FromResult(_counts.Values.Sum());
    }

    public Task<int> CountEnrollmentsByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        CountEnrollmentsByOrgsCalls++;
        var orgs = organizationIds.Distinct().ToList();
        LastBulkOrgIds = orgs;
        return Task.FromResult(orgs.Where(_counts.ContainsKey).Sum(id => _counts[id]));
    }

    // Unused by the tests — contract completeness.
    public Task<AdminEnrollResult> EnrollAsync(Guid studentId, Guid courseId)
        => Task.FromResult(new AdminEnrollResult(Guid.NewGuid(), studentId, courseId, false, DateTimeOffset.UtcNow));
    public Task<IList<AdminEnrollResult>> EnrollManyAsync(Guid courseId, IEnumerable<Guid> studentIds)
        => Task.FromResult<IList<AdminEnrollResult>>(Array.Empty<AdminEnrollResult>());
    public Task<bool> UnenrollAsync(Guid enrollmentId) => Task.FromResult(true);
    public Task<IList<AdminEnrollmentInfo>> GetStudentEnrollmentsAsync(Guid studentId)
        => Task.FromResult<IList<AdminEnrollmentInfo>>(Array.Empty<AdminEnrollmentInfo>());
    public Task<IList<RecentEnrollmentInfo>> GetRecentEnrollmentsAsync(int take)
        => Task.FromResult<IList<RecentEnrollmentInfo>>(Array.Empty<RecentEnrollmentInfo>());
    public Task<IList<AdminEnrollmentInfo>> ListAsync(string? studentName = null, string? courseTitle = null)
        => Task.FromResult<IList<AdminEnrollmentInfo>>(Array.Empty<AdminEnrollmentInfo>());
    public Task<AdminEnrollmentPageResult> ListPagedAsync(
        string? studentName, string? courseTitle, int pageNumber, int pageSize)
        => Task.FromResult(new AdminEnrollmentPageResult(
            new List<AdminEnrollmentRow>(), 0));
}

/// <summary>IUserLookup stub — learner counts are not under test here.</summary>
public class StubUserLookup : IUserLookup
{
    public Task<UserScopeInfo?> GetUserScopeAsync(Guid studentId) => Task.FromResult<UserScopeInfo?>(null);
    public Task<int> CountLearnersAsync(Guid? organizationId = null) => Task.FromResult(0);
    public Task<IList<OrgLearnerCount>> GetLearnerCountsByOrgAsync()
        => Task.FromResult<IList<OrgLearnerCount>>(Array.Empty<OrgLearnerCount>());
    public Task<string?> GetUserNameAsync(Guid studentId) => Task.FromResult<string?>(null);
    public Task<IList<UserSummary>> GetUsersAsync(IEnumerable<Guid> studentIds)
        => Task.FromResult<IList<UserSummary>>(Array.Empty<UserSummary>());
    public Task<int> CountByRoleAsync(string role) => Task.FromResult(0);
}
