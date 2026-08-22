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
    /// <summary>Enroll a single learner in a course.</summary>
    public async Task<AdminEnrollResult> EnrollAsync(Guid studentId, Guid courseId)
    {
        var result = await enrollmentAdmin.EnrollAsync(studentId, courseId);
        if (result.AlreadyEnrolled)
            throw new InvalidOperationException("Student is already enrolled in this course.");
        return result;
    }

    /// <summary>Bulk enroll learners in a course (up to 500).</summary>
    public async Task<BulkEnrollmentResult> BulkEnrollAsync(IList<Guid> studentIds, Guid courseId)
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
                var scope = await userLookup.GetUserScopeAsync(studentId);
                if (scope is null)
                {
                    skipped++;
                    errorMessages.Add($"Student {studentId} not found — skipped.");
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

    /// <summary>Cancel an enrollment.</summary>
    public async Task CancelEnrollmentAsync(Guid enrollmentId)
    {
        var removed = await enrollmentAdmin.UnenrollAsync(enrollmentId);
        if (!removed)
            throw new KeyNotFoundException("Enrollment not found.");
    }

    /// <summary>List all enrollments (SuperUser) with learner and organization info.</summary>
    public async Task<IList<EnrollmentDto>> ListAllEnrollmentsAsync(string? studentName = null, string? courseTitle = null)
    {
        // Step 1: enrollments + course titles from the Enrollment module (contract)
        var rows = await enrollmentAdmin.ListAsync(studentName, courseTitle);

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

            var scope = await userLookup.GetUserScopeAsync(r.StudentId);
            var orgId = scope?.OrganizationId ?? Guid.Empty;

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
        string? studentName, string? courseTitle, int pageNumber, int pageSize)
    {
        var page = await enrollmentAdmin.ListPagedAsync(studentName, courseTitle, pageNumber, pageSize);

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
