using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Tests.Management;

/// <summary>
/// Org-scope enforcement in AdminEnrollmentService (spec 052, ADR 0010).
/// Enrollment scope is one-dimensional: the enrolled student's organization.
///
/// Org tree: Root → OrgA → OrgB; Root → OrgC.
/// </summary>
public class EnrollmentScopeTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid OrgC = Guid.NewGuid();

    private static readonly Guid StudentA = Guid.NewGuid();
    private static readonly Guid StudentC = Guid.NewGuid();
    private static readonly Guid Course = Guid.NewGuid();
    private static readonly Guid OtherCourse = Guid.NewGuid();
    private static readonly Guid EnrollmentOfA = Guid.NewGuid();
    private static readonly Guid EnrollmentOfC = Guid.NewGuid();

    private readonly FakeProvisioning _provisioning = new();
    private readonly FakeOrgLookup _orgLookup = new();
    private readonly FakeEnrollmentAdmin _enrollmentAdmin = new();
    private readonly AdminEnrollmentService _service;

    private static OrgScope SuperUser => OrgScope.SuperUser;
    private static OrgScope AdminOfA => OrgScope.ForOrgAdmin(OrgA);

    public EnrollmentScopeTests()
    {
        _orgLookup.AddOrg(Root, "Root", parentId: null);
        _orgLookup.AddOrg(OrgA, "Org A", parentId: Root);
        _orgLookup.AddOrg(OrgB, "Org B", parentId: OrgA);
        _orgLookup.AddOrg(OrgC, "Org C", parentId: Root);

        _provisioning.Add(new StudentProvisionedDto(StudentA, "Student A", "sa@example.com", "Learner", OrgA, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(StudentC, "Student C", "sc@example.com", "Learner", OrgC, DateTimeOffset.UtcNow, true));

        _enrollmentAdmin.AddEnrollment(EnrollmentOfA, StudentA, Course, "Course", DateTimeOffset.UtcNow);
        _enrollmentAdmin.AddEnrollment(EnrollmentOfC, StudentC, Course, "Course", DateTimeOffset.UtcNow);

        var courseLookup = new SingleCourseLookup(
            new CourseSummary(Course, "Course", "CAT", OrgA),
            new CourseSummary(OtherCourse, "Other Course", "CAT", OrgA));
        _service = new AdminEnrollmentService(
            _enrollmentAdmin,
            new FakeUserLookup(_provisioning),
            courseLookup,
            _orgLookup);
    }

    // ── List ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAll_orgadmin_sees_only_subtree_learners()
    {
        var rows = await _service.ListAllEnrollmentsAsync(null, null, AdminOfA);
        var emails = rows.Select(r => r.StudentEmail).ToList();
        Assert.Contains("sa@example.com", emails);
        Assert.DoesNotContain("sc@example.com", emails);
    }

    [Fact]
    public async Task ListAll_superuser_sees_everything()
    {
        var rows = await _service.ListAllEnrollmentsAsync(null, null, SuperUser);
        var emails = rows.Select(r => r.StudentEmail).ToList();
        Assert.Contains("sa@example.com", emails);
        Assert.Contains("sc@example.com", emails);
    }

    [Fact]
    public async Task ListPaged_orgadmin_passes_subtree_root_to_procedure()
    {
        await _service.ListAllEnrollmentsPagedAsync(null, null, 1, 10, AdminOfA);
        Assert.Equal(OrgA, _enrollmentAdmin.LastRootOrgId);
    }

    [Fact]
    public async Task ListPaged_superuser_passes_null_root()
    {
        await _service.ListAllEnrollmentsPagedAsync(null, null, 1, 10, SuperUser);
        Assert.Null(_enrollmentAdmin.LastRootOrgId);
    }

    // ── Enroll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Enroll_out_of_subtree_student_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.EnrollAsync(StudentC, Course, AdminOfA));
        // No enrollment side effect.
        Assert.Empty(_enrollmentAdmin.Enrolled);
    }

    [Fact]
    public async Task Enroll_in_subtree_student_succeeds()
    {
        // OtherCourse: StudentA is pre-seeded into Course, so a fresh course
        // avoids the existing already-enrolled conflict path.
        var result = await _service.EnrollAsync(StudentA, OtherCourse, AdminOfA);
        Assert.Equal(StudentA, result.StudentId);
    }

    [Fact]
    public async Task Enroll_already_enrolled_throws_invalid_operation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.EnrollAsync(StudentA, Course, AdminOfA));
    }

    [Fact]
    public async Task BulkEnroll_out_of_subtree_students_are_skipped_with_message()
    {
        var result = await _service.BulkEnrollAsync([StudentA, StudentC], OtherCourse, AdminOfA);
        Assert.Equal(1, result.Enrolled);
        Assert.Equal(1, result.Skipped);
        Assert.Contains(result.ErrorMessages, m => m.Contains("outside your organization scope"));
    }

    [Fact]
    public async Task BulkEnroll_superuser_enrolls_everyone()
    {
        var result = await _service.BulkEnrollAsync([StudentA, StudentC], OtherCourse, SuperUser);
        Assert.Equal(2, result.Enrolled);
        Assert.Equal(0, result.Skipped);
    }

    // ── Cancel ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_out_of_subtree_throws_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.CancelEnrollmentAsync(EnrollmentOfC, AdminOfA));
        // The enrollment must still exist (no side effects before the check).
        Assert.Contains(EnrollmentOfC, _enrollmentAdmin.Enrollments.Keys);
    }

    [Fact]
    public async Task Cancel_in_subtree_succeeds()
    {
        await _service.CancelEnrollmentAsync(EnrollmentOfA, AdminOfA);
        Assert.DoesNotContain(EnrollmentOfA, _enrollmentAdmin.Enrollments.Keys);
    }

    [Fact]
    public async Task Cancel_missing_enrollment_throws_key_not_found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CancelEnrollmentAsync(Guid.NewGuid(), SuperUser));
    }
}

/// <summary>ICourseLookup fake with a fixed set of courses.</summary>
internal sealed class SingleCourseLookup(params CourseSummary[] courses) : ICourseLookup
{
    private readonly List<CourseSummary> _courses = [.. courses];

    public Task<CourseSummary?> GetCourseAsync(Guid courseId)
        => Task.FromResult(_courses.FirstOrDefault(c => c.Id == courseId));
    public Task<int> CountAsync() => Task.FromResult(_courses.Count);
    public Task<int> CountByOrgAsync(Guid organizationId)
        => Task.FromResult(_courses.Count(c => c.OrganizationId == organizationId));
    public Task<IReadOnlyDictionary<Guid, int>> GetCourseCountsByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var ids = organizationIds.ToHashSet();
        var dict = _courses.Where(c => ids.Contains(c.OrganizationId))
            .GroupBy(c => c.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(dict);
    }
    public Task<IList<string>> GetDistinctCategoriesAsync()
        => Task.FromResult<IList<string>>(_courses.Select(c => c.Category).Distinct().OrderBy(c => c).ToList());
    public Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds)
    {
        var ids = courseIds.ToHashSet();
        return Task.FromResult<IList<CourseSummary>>(_courses.Where(c => ids.Contains(c.Id)).ToList());
    }
    public Task<IList<CourseSummary>> ListByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var ids = organizationIds.ToHashSet();
        return Task.FromResult<IList<CourseSummary>>(_courses.Where(c => ids.Contains(c.OrganizationId)).ToList());
    }
    public Task<IList<CourseSummary>> ListAllAsync() => Task.FromResult<IList<CourseSummary>>(_courses.ToList());
}

/// <summary>IEnrollmentAdmin fake: in-memory enrollments with student map.</summary>
internal sealed class FakeEnrollmentAdmin : IEnrollmentAdmin
{
    public Dictionary<Guid, (Guid StudentId, Guid CourseId, string Title, DateTimeOffset At)> Enrollments { get; } = new();
    public List<(Guid Student, Guid Course)> Enrolled { get; } = [];
    public Guid? LastRootOrgId { get; private set; }
    public bool LastRootOrgIdWasPassed { get; private set; }

    public void AddEnrollment(Guid enrollmentId, Guid studentId, Guid courseId, string title, DateTimeOffset at)
        => Enrollments[enrollmentId] = (studentId, courseId, title, at);

    public Task<AdminEnrollResult> EnrollAsync(Guid studentId, Guid courseId)
    {
        if (Enrollments.Values.Any(e => e.StudentId == studentId && e.CourseId == courseId))
            return Task.FromResult(new AdminEnrollResult(Guid.Empty, studentId, courseId, true, DateTimeOffset.UtcNow));
        var id = Guid.NewGuid();
        Enrollments[id] = (studentId, courseId, "Course", DateTimeOffset.UtcNow);
        Enrolled.Add((studentId, courseId));
        return Task.FromResult(new AdminEnrollResult(id, studentId, courseId, false, DateTimeOffset.UtcNow));
    }

    public Task<IList<AdminEnrollResult>> EnrollManyAsync(Guid courseId, IEnumerable<Guid> studentIds)
        => Task.FromResult<IList<AdminEnrollResult>>([]);

    public Task<bool> UnenrollAsync(Guid enrollmentId)
    {
        var removed = Enrollments.Remove(enrollmentId);
        return Task.FromResult(removed);
    }

    public Task<Guid?> GetEnrollmentStudentIdAsync(Guid enrollmentId)
        => Task.FromResult(Enrollments.TryGetValue(enrollmentId, out var e) ? e.StudentId : (Guid?)null);

    public Task<IList<AdminEnrollmentInfo>> GetStudentEnrollmentsAsync(Guid studentId)
        => Task.FromResult<IList<AdminEnrollmentInfo>>(
            Enrollments.Where(kv => kv.Value.StudentId == studentId)
                .Select(kv => new AdminEnrollmentInfo(kv.Key, studentId, kv.Value.CourseId, kv.Value.At, kv.Value.Title))
                .ToList());

    public Task<int> CountEnrollmentsAsync(Guid? organizationId = null) => Task.FromResult(Enrollments.Count);

    public Task<int> CountEnrollmentsByOrgsAsync(IEnumerable<Guid> organizationIds)
        => Task.FromResult(Enrollments.Count);

    public Task<IList<RecentEnrollmentInfo>> GetRecentEnrollmentsAsync(int take)
        => Task.FromResult<IList<RecentEnrollmentInfo>>([]);

    public Task<IList<AdminEnrollmentInfo>> ListAsync(string? studentName = null, string? courseTitle = null)
        => Task.FromResult<IList<AdminEnrollmentInfo>>(
            Enrollments
                .Select(kv => new AdminEnrollmentInfo(kv.Key, kv.Value.StudentId, kv.Value.CourseId, kv.Value.At, kv.Value.Title))
                .OrderByDescending(e => e.EnrolledAt)
                .ToList());

    public Task<AdminEnrollmentPageResult> ListPagedAsync(
        string? studentName, string? courseTitle, int pageNumber, int pageSize, Guid? rootOrgId = null)
    {
        LastRootOrgId = rootOrgId;
        LastRootOrgIdWasPassed = true;
        return Task.FromResult(new AdminEnrollmentPageResult(new List<AdminEnrollmentRow>(), 0));
    }
}
