using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;

namespace LibreLms.Modules.Management.Application;

/// <summary>DTO for enrollment listing.</summary>
public record EnrollmentDto(
    Guid EnrollmentId,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    Guid CourseId,
    string CourseTitle,
    string OrganizationName,
    DateTimeOffset EnrolledAt
);

/// <summary>A page of enrollment rows plus the filtered total count (spec 032).</summary>
public record EnrollmentPageResult(IList<EnrollmentDto> Items, int TotalCount);

/// <summary>Result of a bulk enrollment operation.</summary>
public record BulkEnrollmentResult(
    int Enrolled,
    int Skipped,
    int Errors,
    List<string> ErrorMessages
);

/// <summary>
/// Service for admin enrollment management (single and bulk).
/// Spec 027 (R9): all enrollment work delegates to the Enrollment module's IEnrollmentAdmin
/// contract (learner facts via IUserLookup, course existence via ICourseLookup) — this
/// module no longer touches EnrollmentDbContext or CatalogDbContext directly.
/// Behavior is preserved: same exceptions, same skip/error semantics, same DTO shape.
/// </summary>
public class AdminEnrollmentService(
    IEnrollmentAdmin enrollmentAdmin,
    IUserLookup userLookup,
    ICourseLookup courseLookup,
    IOrganizationLookup orgLookup)
{
    /// <summary>True when the learner's organization is within the scope (ADR 0010).
    /// A missing learner fails closed (not in scope).</summary>
    private async Task<bool> StudentOrgInScopeAsync(Guid studentId, OrgScope scope)
    {
        if (scope.IsSuperUser)
            return true;

        var learnerScope = await userLookup.GetUserScopeAsync(studentId);
        return learnerScope is not null &&
            await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, learnerScope.OrganizationId);
    }
    /// <summary>
    /// Enroll a single learner in a course. The student's organization must be
    /// within the caller's scope (ADR 0010).
    /// </summary>
    public async Task<AdminEnrollResult> EnrollAsync(Guid studentId, Guid courseId, OrgScope scope)
    {
        if (!await StudentOrgInScopeAsync(studentId, scope))
            throw new ForbiddenAccessException("This learner is outside your organization scope.");

        var result = await enrollmentAdmin.EnrollAsync(studentId, courseId);
        if (result.AlreadyEnrolled)
            throw new InvalidOperationException("Student is already enrolled in this course.");
        return result;
    }

    /// <summary>
    /// Bulk enroll learners in a course (up to 500). Learners whose organization is
    /// outside the caller's scope are skipped with an error message (ADR 0010).
    /// </summary>
    public async Task<BulkEnrollmentResult> BulkEnrollAsync(IList<Guid> studentIds, Guid courseId, OrgScope scope)
    {
        if (studentIds.Count > 500)
            throw new ArgumentException("Maximum 500 learners per bulk enrollment.", nameof(studentIds));

        // Verify course exists (same up-front check as before)
        var course = await courseLookup.GetCourseAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException("Course not found.");

        var enrolled = 0;
        var skipped = 0;
        var errors = 0;
        var errorMessages = new List<string>();

        foreach (var studentId in studentIds)
        {
            try
            {
                var learnerScope = await userLookup.GetUserScopeAsync(studentId);
                if (learnerScope is null)
                {
                    skipped++;
                    errorMessages.Add($"Student {studentId} not found — skipped.");
                    continue;
                }

                // ADR 0010: out-of-scope learners are refused per student.
                if (!await OrgSubtree.IsOrgInScopeAsync(scope, orgLookup, learnerScope.OrganizationId))
                {
                    skipped++;
                    errorMessages.Add($"Student {studentId} is outside your organization scope — skipped.");
                    continue;
                }

                var result = await enrollmentAdmin.EnrollAsync(studentId, courseId);
                if (result.AlreadyEnrolled)
                {
                    skipped++;
                    continue;
                }

                enrolled++;
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"Error enrolling student {studentId}: {ex.Message}");
            }
        }

        return new BulkEnrollmentResult(enrolled, skipped, errors, errorMessages);
    }

    /// <summary>
    /// Cancel an enrollment. The enrolled student's organization must be within
    /// the caller's scope (ADR 0010).
    /// </summary>
    public async Task CancelEnrollmentAsync(Guid enrollmentId, OrgScope scope)
    {
        var studentId = await enrollmentAdmin.GetEnrollmentStudentIdAsync(enrollmentId);
        if (studentId is null)
            throw new KeyNotFoundException("Enrollment not found.");

        if (!await StudentOrgInScopeAsync(studentId.Value, scope))
            throw new ForbiddenAccessException("This learner is outside your organization scope.");

        var removed = await enrollmentAdmin.UnenrollAsync(enrollmentId);
        if (!removed)
            throw new KeyNotFoundException("Enrollment not found.");
    }

    /// <summary>
    /// List all enrollments with learner and organization info. SuperUser sees
    /// everything; an OrgAdmin sees only enrollments of learners in their subtree
    /// (ADR 0010).
    /// </summary>
    public async Task<IList<EnrollmentDto>> ListAllEnrollmentsAsync(string? studentName, string? courseTitle, OrgScope scope)
    {
        // Step 1: enrollments + course titles from the Enrollment module (contract)
        var rows = await enrollmentAdmin.ListAsync(studentName, courseTitle);

        // ADR 0010: rows whose learner org is outside the caller's subtree are
        // dropped in the loop below (org membership is resolved per learner).
        var subtree = await OrgSubtree.GetSubtreeOrgIdsAsync(scope, orgLookup);

        // Step 2: learner names/emails in one batch (contract)
        var students = await userLookup.GetUsersAsync(rows.Select(r => r.StudentId));
        var studentMap = students.ToDictionary(s => s.Id);

        // Step 3: org names from the Management contract (own module boundary)
        var orgCache = new Dictionary<Guid, string>();
        var dtos = new List<EnrollmentDto>();

        foreach (var r in rows)
        {
            if (!studentMap.TryGetValue(r.StudentId, out var student))
                continue;

            var learnerScope = await userLookup.GetUserScopeAsync(r.StudentId);
            var orgId = learnerScope?.OrganizationId ?? Guid.Empty;

            // ADR 0010: out-of-subtree learner rows are not listed.
            if (subtree is not null && !subtree.Contains(orgId))
                continue;

            if (!orgCache.TryGetValue(orgId, out var orgName))
            {
                var org = await orgLookup.GetOrganizationAsync(orgId);
                orgName = org?.Name ?? "Unknown";
                orgCache[orgId] = orgName;
            }

            dtos.Add(new EnrollmentDto(
                r.EnrollmentId,
                r.StudentId,
                student.Name,
                student.Email,
                r.CourseId,
                r.CourseTitle,
                orgName,
                r.EnrolledAt
            ));
        }

        return dtos;
    }

    /// <summary>
    /// Paged variant of ListAllEnrollmentsAsync: delegates to IEnrollmentAdmin.ListPagedAsync,
    /// then enriches org names for the page's distinct OrganizationIds via IOrganizationLookup
    /// (page-local cache, bounded to the page). The row already carries learner name/email —
    /// no IUserLookup call (spec 032).
    /// </summary>
    public async Task<EnrollmentPageResult> ListAllEnrollmentsPagedAsync(
        string? studentName, string? courseTitle, int pageNumber, int pageSize, OrgScope scope)
    {
        // ADR 0010: the paged stored procedure filters to the scope's root org
        // (SuperUser passes null for system-wide).
        var rootOrgId = scope.IsSuperUser ? null : scope.OrganizationId;
        var page = await enrollmentAdmin.ListPagedAsync(studentName, courseTitle, pageNumber, pageSize, rootOrgId);

        // Org names for the page's distinct OrganizationIds (page-local cache; missing org -> "Unknown").
        var orgCache = new Dictionary<Guid, string>();
        var dtos = new List<EnrollmentDto>();

        foreach (var r in page.Items)
        {
            if (!orgCache.TryGetValue(r.OrganizationId, out var orgName))
            {
                var org = await orgLookup.GetOrganizationAsync(r.OrganizationId);
                orgName = org?.Name ?? "Unknown";
                orgCache[r.OrganizationId] = orgName;
            }

            dtos.Add(new EnrollmentDto(
                r.EnrollmentId,
                r.StudentId,
                r.StudentName,
                r.StudentEmail,
                r.CourseId,
                r.CourseTitle,
                orgName,
                r.EnrolledAt
            ));
        }

        return new EnrollmentPageResult(dtos, page.TotalCount);
    }
}
