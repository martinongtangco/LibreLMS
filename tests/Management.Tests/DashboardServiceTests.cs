using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;
using LibreLms.Contracts.Scorm;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Infrastructure;
using LibreLms.SharedKernel;
using Microsoft.EntityFrameworkCore;
using UserScopeInfo = LibreLms.Contracts.Enrollment.UserScopeInfo;

namespace LibreLms.Tests.Management;

/// <summary>
/// DashboardService metric wiring (spec 058 US1/US2): completion rate, completed-course
/// count, and average score come from real SCORM attempt aggregates (IScormAttemptStats),
/// not hardcoded zeros. The org view's subtree aggregation (spec 052's OrgSubtree rule
/// across three cross-module contracts) is covered with hand-computed expectations.
///
/// Org tree: Root → OrgA → OrgB;  Root → OrgD (empty).
/// </summary>
public class DashboardServiceTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid OrgD = Guid.NewGuid();

    private static readonly Guid S1 = Guid.NewGuid();
    private static readonly Guid S2 = Guid.NewGuid();
    private static readonly Guid S3 = Guid.NewGuid();
    private static readonly Guid S4 = Guid.NewGuid();   // in Root — outside OrgA's subtree
    private static readonly Guid P1 = Guid.NewGuid();   // personal-view learner

    private static readonly Guid C1 = Guid.NewGuid();
    private static readonly Guid C2 = Guid.NewGuid();
    private static readonly Guid C3 = Guid.NewGuid();

    private static readonly Guid E1 = Guid.NewGuid();
    private static readonly Guid E2 = Guid.NewGuid();
    private static readonly Guid E3 = Guid.NewGuid();
    private static readonly Guid E4 = Guid.NewGuid();

    private readonly ManagementDbContext _db;
    private readonly FakeProvisioning _provisioning = new();
    private readonly DashUserLookup _userLookup;
    private readonly FakeEnrollmentAdmin _enrollmentAdmin = new();
    private readonly SingleCourseLookup _courseLookup;
    private readonly DashScormAttemptStats _scormStats = new();
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase($"DashMetricsTests_{Guid.NewGuid()}")
            .Options;
        _db = new ManagementDbContext(options);

        _db.Organizations.Add(new Organization { Id = Root, Name = "Root", ParentId = null });
        _db.Organizations.Add(new Organization { Id = OrgA, Name = "Org A", ParentId = Root });
        _db.Organizations.Add(new Organization { Id = OrgB, Name = "Org B", ParentId = OrgA });
        _db.Organizations.Add(new Organization { Id = OrgD, Name = "Org D", ParentId = Root });
        _db.SaveChanges();

        // Learners: OrgA has S1+S2, OrgB has S3 (OrgA subtree), Root has S4 (outside).
        _provisioning.Add(new StudentProvisionedDto(S1, "S1", "s1@example.com", RoleNames.Learner, OrgA, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(S2, "S2", "s2@example.com", RoleNames.Learner, OrgA, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(S3, "S3", "s3@example.com", RoleNames.Learner, OrgB, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(S4, "S4", "s4@example.com", RoleNames.Learner, Root, DateTimeOffset.UtcNow, true));
        _provisioning.Add(new StudentProvisionedDto(P1, "P1", "p1@example.com", RoleNames.Learner, OrgA, DateTimeOffset.UtcNow, true));

        _userLookup = new DashUserLookup(_provisioning);
        _enrollmentAdmin.AddEnrollment(E1, S1, C1, "Course 1", DateTimeOffset.UtcNow);
        _enrollmentAdmin.AddEnrollment(E2, S2, C2, "Course 2", DateTimeOffset.UtcNow);
        _enrollmentAdmin.AddEnrollment(E3, S3, C1, "Course 1", DateTimeOffset.UtcNow);
        _enrollmentAdmin.AddEnrollment(E4, P1, C1, "Course 1", DateTimeOffset.UtcNow);

        _courseLookup = new SingleCourseLookup(
            new CourseSummary(C1, "Course 1", "CAT", OrgA),
            new CourseSummary(C2, "Course 2", "CAT", OrgB),
            new CourseSummary(C3, "Course 3", "CAT", Root));

        // Real OrganizationLookup over the same InMemory context: the shared subtree
        // rule (ADR 0010) runs for real, not faked (same pattern as
        // Enrollment.Tests/DashboardServiceBulkCountsTests).
        _service = new DashboardService(
            _db,
            _userLookup,
            _enrollmentAdmin,
            _courseLookup,
            new OrganizationLookup(_db),
            _provisioning,
            _scormStats);
    }

    // ── Org view (US1-1 + US2-1: hand-computed subtree aggregate) ─────────

    [Fact]
    public async Task GetOrgMetrics_subtree_aggregates_real_attempt_stats()
    {
        // Scorm data for OrgA's subtree learners {S1, S2, P1, S3}: 5 attempts, 2
        // completed (both scored, 100 + 75, same course) → completion rate 2/5 = 0.4.
        _scormStats.SetForStudents(new[] { S1, S2, P1, S3 },
            new AttemptStatsSummary(TotalAttempts: 5, CompletedAttempts: 2,
                ScoredCompletedAttempts: 2, CompletedScoreSum: 175.0, DistinctCompletedCourses: 1));

        var metrics = await _service.GetOrgMetricsAsync(OrgA);

        // Hand-computed:
        //  OrganizationCount = subtree {OrgA, OrgB} minus the org itself = 1
        //  LearnerCount      = OrgA(S1, S2, P1) + OrgB(S3) = 4   (S4 in Root excluded)
        //  CourseCount       = OrgA(1) + OrgB(1) = 2             (C3 in Root excluded)
        //  EnrollmentCount   = 4 seeded enrollments (fake counts the full set)
        //  AverageCompletionRate = 2/5 = 0.4
        Assert.Equal(1, metrics.OrganizationCount);
        Assert.Equal(4, metrics.LearnerCount);
        Assert.Equal(2, metrics.CourseCount);
        Assert.Equal(4, metrics.EnrollmentCount);
        Assert.Equal(0.4, metrics.AverageCompletionRate, precision: 10);
        Assert.Equal("Org A", metrics.OrganizationName);

        // The Scorm contract must have received exactly the subtree's learners
        // (S4 in Root excluded — the subtree scoping happened before the boundary).
        Assert.Equal(new HashSet<Guid> { S1, S2, P1, S3 }, _scormStats.LastRequestedStudentIds);
    }

    [Fact]
    public async Task GetOrgMetrics_empty_subtree_reads_zero_from_real_data()
    {
        // OrgD has no learners → empty student set → zero summary, rate 0, no throw
        // (spec edge case: zero members must not divide by zero).
        var metrics = await _service.GetOrgMetricsAsync(OrgD);

        Assert.Equal(0, metrics.AverageCompletionRate);
        Assert.Equal(0, metrics.LearnerCount);
        Assert.Empty(_scormStats.LastRequestedStudentIds ?? []);
    }

    // ── System view ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSystemMetrics_uses_real_attempt_stats()
    {
        _scormStats.SetSystemStats(new AttemptStatsSummary(TotalAttempts: 10, CompletedAttempts: 3,
            ScoredCompletedAttempts: 3, CompletedScoreSum: 240.0, DistinctCompletedCourses: 2));

        var metrics = await _service.GetSystemMetricsAsync();

        Assert.Equal(0.3, metrics.AverageCompletionRate, precision: 10);
        Assert.Equal(4, metrics.TotalOrganizations);
        Assert.Equal(5, metrics.TotalLearners);
        Assert.Equal(3, metrics.TotalCourses);
        Assert.Equal(4, metrics.TotalEnrollments);
    }

    [Fact]
    public async Task GetSystemMetrics_zero_attempts_reads_zero()
    {
        _scormStats.SetSystemStats(new AttemptStatsSummary(0, 0, 0, 0.0, 0));

        var metrics = await _service.GetSystemMetricsAsync();

        Assert.Equal(0.0, metrics.AverageCompletionRate);
    }

    // ── Personal view (US1-1) ──────────────────────────────────────────────

    [Fact]
    public async Task GetPersonalMetrics_one_completed_scored_attempt_reflects_it()
    {
        _scormStats.SetForStudents(new[] { P1 },
            new AttemptStatsSummary(TotalAttempts: 1, CompletedAttempts: 1,
                ScoredCompletedAttempts: 1, CompletedScoreSum: 87.5, DistinctCompletedCourses: 1));

        var metrics = await _service.GetPersonalMetricsAsync(P1);

        Assert.Equal(1, metrics.CompletedCourseCount);
        Assert.Equal(87.5, metrics.AverageScore, precision: 10);
        Assert.Equal(1, metrics.EnrolledCourseCount);
        Assert.Equal("P1", metrics.LearnerName);
        Assert.Equal(new HashSet<Guid> { P1 }, _scormStats.LastRequestedStudentIds);
    }

    [Fact]
    public async Task GetPersonalMetrics_no_attempts_reads_zero_without_divide_by_zero()
    {
        _scormStats.SetForStudents(new[] { P1 },
            new AttemptStatsSummary(TotalAttempts: 0, CompletedAttempts: 0,
                ScoredCompletedAttempts: 0, CompletedScoreSum: 0.0, DistinctCompletedCourses: 0));

        var metrics = await _service.GetPersonalMetricsAsync(P1);

        Assert.Equal(0, metrics.CompletedCourseCount);
        Assert.Equal(0.0, metrics.AverageScore);
    }

    [Fact]
    public async Task GetPersonalMetrics_attempts_but_none_completed_reads_zero()
    {
        // Spec edge case: attempts exist but none are completed → 0% from real data.
        _scormStats.SetForStudents(new[] { P1 },
            new AttemptStatsSummary(TotalAttempts: 3, CompletedAttempts: 0,
                ScoredCompletedAttempts: 0, CompletedScoreSum: 0.0, DistinctCompletedCourses: 0));

        var metrics = await _service.GetPersonalMetricsAsync(P1);

        Assert.Equal(0, metrics.CompletedCourseCount);
        Assert.Equal(0.0, metrics.AverageScore);
    }

    [Fact]
    public async Task GetPersonalMetrics_unknown_student_throws_key_not_found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetPersonalMetricsAsync(Guid.NewGuid()));
    }

    // ── Fakes (this file; no collision with the shared fakes above) ────────

    /// <summary>IUserLookup fake with real per-org count semantics (groups by org).</summary>
    internal sealed class DashUserLookup(FakeProvisioning provisioning) : IUserLookup
    {
        public Task<UserScopeInfo?> GetUserScopeAsync(Guid studentId)
        {
            var s = provisioning.GetByIdAsync(studentId).GetAwaiter().GetResult();
            return Task.FromResult(s is null ? null : new UserScopeInfo(s.OrganizationId, s.Role));
        }

        public Task<int> CountLearnersAsync(Guid? organizationId = null)
        {
            var all = provisioning.ListAsync(null).GetAwaiter().GetResult();
            return Task.FromResult(organizationId.HasValue
                ? all.Count(s => s.OrganizationId == organizationId.Value)
                : all.Count);
        }

        public Task<IList<OrgLearnerCount>> GetLearnerCountsByOrgAsync()
        {
            var all = provisioning.ListAsync(null).GetAwaiter().GetResult();
            return Task.FromResult<IList<OrgLearnerCount>>(
                all.GroupBy(s => s.OrganizationId)
                    .Select(g => new OrgLearnerCount(g.Key, g.Count()))
                    .ToList());
        }

        public Task<string?> GetUserNameAsync(Guid studentId)
        {
            var s = provisioning.GetByIdAsync(studentId).GetAwaiter().GetResult();
            return Task.FromResult<string?>(s?.Name);
        }

        public Task<IList<UserSummary>> GetUsersAsync(IEnumerable<Guid> studentIds)
        {
            var ids = studentIds.ToHashSet();
            var all = provisioning.ListAsync(null).GetAwaiter().GetResult();
            return Task.FromResult<IList<UserSummary>>(
                all.Where(s => ids.Contains(s.Id))
                    .Select(s => new UserSummary(s.Id, s.Name, s.Email, s.OrganizationId))
                    .ToList());
        }

        public Task<int> CountByRoleAsync(string role)
        {
            var all = provisioning.ListAsync(null).GetAwaiter().GetResult();
            return Task.FromResult(all.Count(s => s.Role == role));
        }
    }

    /// <summary>IScormAttemptStats fake: pre-configured summaries; records the requested
    /// student set so tests can prove the correct learners were passed across the boundary.</summary>
    internal sealed class DashScormAttemptStats : IScormAttemptStats
    {
        private AttemptStatsSummary _system = new(0, 0, 0, 0.0, 0);
        // Keyed by a canonical string — HashSet<Guid> has reference equality and
        // cannot serve as a dictionary key by content.
        private readonly Dictionary<string, AttemptStatsSummary> _byStudents = new();

        public HashSet<Guid>? LastRequestedStudentIds { get; private set; }

        private static string Key(IEnumerable<Guid> ids) => string.Join("|", ids.OrderBy(i => i));

        public void SetSystemStats(AttemptStatsSummary summary) => _system = summary;

        public void SetForStudents(IEnumerable<Guid> studentIds, AttemptStatsSummary summary)
            => _byStudents[Key(studentIds)] = summary;

        public Task<AttemptStatsSummary> GetSystemStatsAsync()
            => Task.FromResult(_system);

        public Task<AttemptStatsSummary> GetStatsForStudentsAsync(IEnumerable<Guid> studentIds)
        {
            var ids = (studentIds ?? []).ToHashSet();
            LastRequestedStudentIds = ids;
            if (ids.Count == 0)
                return Task.FromResult(new AttemptStatsSummary(0, 0, 0, 0.0, 0));
            return Task.FromResult(_byStudents.GetValueOrDefault(Key(ids), new AttemptStatsSummary(0, 0, 0, 0.0, 0)));
        }
    }
}
