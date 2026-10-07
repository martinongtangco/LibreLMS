using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;
using LibreLms.Contracts.Scorm;
using LibreLms.Modules.Management.Infrastructure;

namespace LibreLms.Modules.Management.Application;

/// <summary>DTO for system-wide dashboard metrics.</summary>
public record SystemMetricsDto(
    int TotalOrganizations,
    int TotalLearners,
    int TotalCourses,
    int TotalEnrollments,
    double AverageCompletionRate
);

/// <summary>DTO for org-scoped dashboard metrics.</summary>
public record OrgMetricsDto(
    int OrganizationCount,
    int LearnerCount,
    int CourseCount,
    int EnrollmentCount,
    double AverageCompletionRate,
    string OrganizationName
);

/// <summary>DTO for personal learner metrics.</summary>
public record PersonalMetricsDto(
    int EnrolledCourseCount,
    int CompletedCourseCount,
    double AverageScore,
    string LearnerName
);

/// <summary>DTO for a recent activity entry.</summary>
public record RecentActivityDto(
    string Description,
    DateTimeOffset OccurredAt,
    string ActivityType
);

/// <summary>
/// Service for aggregating dashboard metrics at different organizational scopes.
/// Spec 027 (R9): cross-module facts come from contracts (IUserLookup, IEnrollmentAdmin,
/// ICourseLookup) — only Management-owned data (organizations) comes from ManagementDbContext.
/// Counts keep the pre-existing semantics exactly (all Student rows, org subtree = sum of
/// per-org counts). Spec 058 US1: completion/score metrics come from real SCORM attempt
/// aggregates (IScormAttemptStats) instead of hardcoded zeros.
/// </summary>
public class DashboardService(
    ManagementDbContext managementCtx,
    IUserLookup userLookup,
    IEnrollmentAdmin enrollmentAdmin,
    ICourseLookup courseLookup,
    IOrganizationLookup orgLookup,
    IUserProvisioning userProvisioning,
    IScormAttemptStats scormAttemptStats)
{
    /// <summary>Get system-wide metrics (SuperUser only).</summary>
    public async Task<SystemMetricsDto> GetSystemMetricsAsync()
    {
        var totalOrgs = await managementCtx.Organizations.CountAsync(o => !o.IsDeleted);
        var totalLearners = await userLookup.CountLearnersAsync();
        var totalCourses = await courseLookup.CountAsync();
        var totalEnrollments = await enrollmentAdmin.CountEnrollmentsAsync();

        // Spec 058 US1: real completion rate from SCORM attempt data (Scorm module,
        // via contracts — Principle III). 0.0 when the platform has no attempts.
        var stats = await scormAttemptStats.GetSystemStatsAsync();
        var avgCompletionRate = CompletionRate(stats);

        return new SystemMetricsDto(totalOrgs, totalLearners, totalCourses, totalEnrollments, avgCompletionRate);
    }

    /// <summary>Get metrics scoped to an organization and its descendants.</summary>
    public async Task<OrgMetricsDto> GetOrgMetricsAsync(Guid orgId)
    {
        // Get all descendant org IDs (shared subtree rule, ADR 0010): the org
        // plus all live descendants — same set as the previous private BFS.
        var descendantIds = await OrgSubtree.GetSubtreeOrgIdsAsync(OrgScope.ForOrgAdmin(orgId), orgLookup)
            ?? new HashSet<Guid>();

        var orgName = await managementCtx.Organizations
            .Where(o => o.Id == orgId && !o.IsDeleted)
            .Select(o => o.Name)
            .FirstOrDefaultAsync();

        // Subtree = sum of the per-org counts (dev scale: a handful of orgs).
        var learnerCounts = await userLookup.GetLearnerCountsByOrgAsync();
        var learnerCount = learnerCounts
            .Where(c => descendantIds.Contains(c.OrganizationId))
            .Sum(c => c.Count);

        // Subtree counts in two bulk queries (spec 048 E7) — sum of the per-org counts.
        var courseCounts = await courseLookup.GetCourseCountsByOrgsAsync(descendantIds);
        var courseCount = courseCounts.Values.Sum();
        var enrollmentCount = await enrollmentAdmin.CountEnrollmentsByOrgsAsync(descendantIds);

        // Spec 058 US1: completion rate over the subtree's learners' attempts. Student
        // ids come from the provisioning contract per org (dev scale: a handful of orgs —
        // same per-org pattern as the counts above); the Scorm module scopes by student
        // set (it knows nothing about organizations, Principle III).
        var studentIds = new HashSet<Guid>();
        foreach (var subtreeOrgId in descendantIds)
        {
            foreach (var student in await userProvisioning.ListByOrgAsync(subtreeOrgId))
                studentIds.Add(student.Id);
        }
        var stats = await scormAttemptStats.GetStatsForStudentsAsync(studentIds);

        return new OrgMetricsDto(
            descendantIds.Count - 1, // Exclude the org itself from the count
            learnerCount,
            courseCount,
            enrollmentCount,
            CompletionRate(stats),
            orgName ?? "Unknown");
    }

    /// <summary>Get personal metrics for a learner.</summary>
    public async Task<PersonalMetricsDto> GetPersonalMetricsAsync(Guid studentId)
    {
        var learnerName = await userLookup.GetUserNameAsync(studentId);
        if (learnerName is null)
            throw new KeyNotFoundException("Student not found.");

        var enrollments = await enrollmentAdmin.GetStudentEnrollmentsAsync(studentId);
        var enrolledCount = enrollments.Count;

        // Spec 058 US1: completed courses + average score from the learner's real SCORM
        // attempts (completed = terminal lesson statuses, per the Scorm module's contract).
        var stats = await scormAttemptStats.GetStatsForStudentsAsync(new[] { studentId });
        var completedCount = stats.DistinctCompletedCourses;
        var avgScore = stats.ScoredCompletedAttempts == 0
            ? 0.0
            : stats.CompletedScoreSum / stats.ScoredCompletedAttempts;

        return new PersonalMetricsDto(enrolledCount, completedCount, avgScore, learnerName);
    }

    /// <summary>
    /// Completed-attempts-over-total-attempts, 0.0 when there is no attempt data
    /// (zero from real data — never a divide-by-zero, spec 058 edge case).
    /// </summary>
    private static double CompletionRate(AttemptStatsSummary stats)
        => stats.TotalAttempts == 0 ? 0.0 : (double)stats.CompletedAttempts / stats.TotalAttempts;

    /// <summary>Get recent activity entries.</summary>
    public async Task<IList<RecentActivityDto>> GetRecentActivityAsync(int limit = 10)
    {
        var activities = new List<RecentActivityDto>();

        // Recent enrollments via the Enrollment module's contract (course titles resolved there)
        var recentEnrollments = await enrollmentAdmin.GetRecentEnrollmentsAsync(limit);
        foreach (var e in recentEnrollments)
        {
            activities.Add(new RecentActivityDto(
                $"{e.StudentName} enrolled in {e.CourseTitle}",
                e.EnrolledAt,
                "enrollment"
            ));
        }

        // Recent organizations (Management-owned)
        var recentOrgs = await managementCtx.Organizations
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();

        foreach (var o in recentOrgs)
        {
            activities.Add(new RecentActivityDto(
                $"Organization '{o.Name}' created",
                o.CreatedAt,
                "organization"
            ));
        }

        // Sort all activities and take top N
        return activities
            .OrderByDescending(a => a.OccurredAt)
            .Take(limit)
            .ToList();
    }
}
