using System.IO;
using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Management;
using LibreLms.Contracts.Scorm;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibreLms.Tests.Management;

/// <summary>
/// Org-scope enforcement in CourseVisibilityService (spec 052, ADR 0010).
///
/// Org tree: Root → OrgA → OrgB; Root → OrgC. Courses are owned by orgs.
/// </summary>
public class CourseVisibilityScopeTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid OrgC = Guid.NewGuid();

    private static readonly Guid CourseInA = Guid.NewGuid();
    private static readonly Guid CourseInC = Guid.NewGuid();
    private static readonly Guid CourseInRoot = Guid.NewGuid();

    private readonly ManagementDbContext _db;
    private readonly NoCoursesLookup _courseLookup = new();
    private readonly CourseVisibilityService _service;

    private static OrgScope SuperUser => OrgScope.SuperUser;
    private static OrgScope AdminOfA => OrgScope.ForOrgAdmin(OrgA);

    public CourseVisibilityScopeTests()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase($"CourseVisScopeTests_{Guid.NewGuid()}")
            .Options;
        _db = new ManagementDbContext(options);

        _db.Organizations.Add(new Organization { Id = Root, Name = "Root", ParentId = null });
        _db.Organizations.Add(new Organization { Id = OrgA, Name = "Org A", ParentId = Root });
        _db.Organizations.Add(new Organization { Id = OrgB, Name = "Org B", ParentId = OrgA });
        _db.Organizations.Add(new Organization { Id = OrgC, Name = "Org C", ParentId = Root });
        _db.SaveChanges();

        _courseLookup.Courses.Add(new CourseSummary(CourseInA, "Course in A", "CAT", OrgA));
        _courseLookup.Courses.Add(new CourseSummary(CourseInC, "Course in C", "CAT", OrgC));
        _courseLookup.Courses.Add(new CourseSummary(CourseInRoot, "Course in Root", "CAT", Root));

        _service = new CourseVisibilityService(
            _db,
            _courseLookup,
            new NoOpCourseAdmin(),
            new OrganizationLookup(_db),
            new NoOpScormPackageService());
    }

    // ── GetAllCourses ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCourses_superuser_sees_every_course()
    {
        var courses = await _service.GetAllCoursesAsync(SuperUser);
        var ids = courses.Select(c => c.CourseId).ToHashSet();
        Assert.Contains(CourseInA, ids);
        Assert.Contains(CourseInC, ids);
        Assert.Contains(CourseInRoot, ids);
    }

    [Fact]
    public async Task GetAllCourses_orgadmin_sees_only_subtree_courses()
    {
        var courses = await _service.GetAllCoursesAsync(AdminOfA);
        var ids = courses.Select(c => c.CourseId).ToHashSet();
        Assert.Contains(CourseInA, ids); // own org
        Assert.DoesNotContain(CourseInC, ids);    // sibling org
        Assert.DoesNotContain(CourseInRoot, ids); // ancestor org
    }

    // ── SetVisibilityOverride ──────────────────────────────────────────────

    [Fact]
    public async Task SetVisibilityOverride_for_foreign_org_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.SetVisibilityOverrideAsync(OrgC, CourseInRoot, true, null, AdminOfA));
    }

    [Fact]
    public async Task SetVisibilityOverride_for_subtree_org_succeeds()
    {
        // CourseInRoot is owned by Root (ancestor of OrgA) → overridable in OrgA.
        var @override = await _service.SetVisibilityOverrideAsync(OrgA, CourseInRoot, true, null, AdminOfA);
        Assert.Equal(OrgA, @override.OrganizationId);
        Assert.True(@override.IsHidden);
    }

    // ── GetVisibleCourses / GetOverrides ───────────────────────────────────

    [Fact]
    public async Task GetVisibleCourses_foreign_org_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.GetVisibleCoursesAsync(OrgC, AdminOfA));
    }

    [Fact]
    public async Task GetVisibleCourses_own_org_works()
    {
        var courses = await _service.GetVisibleCoursesAsync(OrgA, AdminOfA);
        Assert.Contains(courses, c => c.CourseId == CourseInA);
    }

    [Fact]
    public async Task GetOverrides_foreign_org_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.GetOverridesAsync(OrgC, AdminOfA));
    }

    // ── DeleteCourse ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCourse_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.DeleteCourseAsync(CourseInC, AdminOfA));
    }

    [Fact]
    public async Task DeleteCourse_in_subtree_succeeds()
    {
        await _service.DeleteCourseAsync(CourseInA, AdminOfA);
        _courseLookup.Courses.Remove(_courseLookup.Courses.First(c => c.Id == CourseInA));
    }
}

/// <summary>ICourseAdmin fake — records deletes; never throws.</summary>
internal sealed class NoOpCourseAdmin : ICourseAdmin
{
    public List<Guid> Deleted { get; } = [];
    public Task<bool> DeleteAsync(Guid courseId)
    {
        Deleted.Add(courseId);
        return Task.FromResult(true);
    }
}

/// <summary>IScormPackageService stub — package side effects are not under test here.</summary>
internal sealed class NoOpScormPackageService : IScormPackageService
{
    public Task<bool> HasPackageAsync(Guid courseId) => Task.FromResult(false);
    public Task<IReadOnlyCollection<Guid>> GetCourseIdsWithPackagesAsync(IEnumerable<Guid> courseIds)
        => Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
    public Task<string?> GetContentDirectoryAsync(Guid courseId) => Task.FromResult<string?>(null);
    public Task DeletePackageForCourseAsync(Guid courseId) => Task.CompletedTask;
    public Task DeleteAsync(Guid packageId) => Task.CompletedTask;
    public Task<(object? Package, string? Error)> UploadAsync(Stream zipStream, Guid? courseId)
        => Task.FromResult<(object?, string?)>((null, null));
    public Task AssociateWithCourseAsync(Guid packageId, Guid courseId) => Task.CompletedTask;
    public Task<(object? Package, string? Error)> ReplacePackageAsync(Guid courseId, Stream zipStream)
        => Task.FromResult<(object?, string?)>((null, null));
    public Task<object?> GetPackageByCourseIdAsync(Guid courseId) => Task.FromResult<object?>(null);
    public Task<IEnumerable<object>> ListAvailableAsync()
        => Task.FromResult<IEnumerable<object>>(Array.Empty<object>());
}
