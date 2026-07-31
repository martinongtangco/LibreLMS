using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Catalog.Infrastructure;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.Modules.Management.Infrastructure;
using DomainEnrollment = LibreLms.Modules.Enrollment.Domain.Enrollment;

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

/// <summary>Result of a bulk enrollment operation.</summary>
public record BulkEnrollmentResult(
    int Enrolled,
    int Skipped,
    int Errors,
    List<string> ErrorMessages
);

/// <summary>Service for admin enrollment management (single and bulk).</summary>
public class AdminEnrollmentService(
    EnrollmentDbContext enrollmentCtx,
    CatalogDbContext catalogCtx,
    ManagementDbContext managementCtx)
{
    /// <summary>Enroll a single learner in a course.</summary>
    public async Task<DomainEnrollment> EnrollAsync(Guid studentId, Guid courseId)
    {
        // Verify student exists
        var student = await enrollmentCtx.Students.FindAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("Student not found.");

        // Verify course exists
        var course = await catalogCtx.Courses.FindAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException("Course not found.");

        // Check for duplicate enrollment
        var existing = await enrollmentCtx.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
        if (existing)
            throw new InvalidOperationException("Student is already enrolled in this course.");

        var enrollment = new DomainEnrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            EnrolledAt = DateTimeOffset.UtcNow
        };

        enrollmentCtx.Enrollments.Add(enrollment);
        await enrollmentCtx.SaveChangesAsync();
        return enrollment;
    }

    /// <summary>Bulk enroll learners in a course (up to 500).</summary>
    public async Task<BulkEnrollmentResult> BulkEnrollAsync(IList<Guid> studentIds, Guid courseId)
    {
        if (studentIds.Count > 500)
            throw new ArgumentException("Maximum 500 learners per bulk enrollment.", nameof(studentIds));

        // Verify course exists
        var course = await catalogCtx.Courses.FindAsync(courseId);
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
                var student = await enrollmentCtx.Students.FindAsync(studentId);
                if (student is null)
                {
                    skipped++;
                    errorMessages.Add($"Student {studentId} not found — skipped.");
                    continue;
                }

                var existing = await enrollmentCtx.Enrollments
                    .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
                if (existing)
                {
                    skipped++;
                    continue;
                }

                var enrollment = new DomainEnrollment
                {
                    StudentId = studentId,
                    CourseId = courseId,
                    EnrolledAt = DateTimeOffset.UtcNow
                };
                enrollmentCtx.Enrollments.Add(enrollment);
                enrolled++;
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"Error enrolling student {studentId}: {ex.Message}");
            }
        }

        if (enrolled > 0)
            await enrollmentCtx.SaveChangesAsync();

        return new BulkEnrollmentResult(enrolled, skipped, errors, errorMessages);
    }

    /// <summary>Cancel an enrollment.</summary>
    public async Task CancelEnrollmentAsync(Guid enrollmentId)
    {
        var enrollment = await enrollmentCtx.Enrollments.FindAsync(enrollmentId);
        if (enrollment is null)
            throw new KeyNotFoundException("Enrollment not found.");

        enrollmentCtx.Enrollments.Remove(enrollment);
        await enrollmentCtx.SaveChangesAsync();
    }

    /// <summary>List enrollments scoped to organization subtree.</summary>
    public async Task<IList<EnrollmentDto>> ListEnrollmentsAsync(IList<Guid> orgIds, string? studentName = null, string? courseTitle = null)
    {
        var query = enrollmentCtx.Enrollments
            .Join(
                enrollmentCtx.Students,
                e => e.StudentId,
                s => s.Id,
                (e, s) => new { Enrollment = e, Student = s })
            .Where(x => orgIds.Contains(x.Student.OrganizationId))
            .Join(
                catalogCtx.Courses,
                x => x.Enrollment.CourseId,
                c => c.Id,
                (x, c) => new { x.Enrollment, x.Student, Course = c });

        if (!string.IsNullOrWhiteSpace(studentName))
        {
            var term = studentName.ToLowerInvariant();
            query = query.Where(x => x.Student.Name.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(courseTitle))
        {
            var term = courseTitle.ToLowerInvariant();
            query = query.Where(x => x.Course.Title.ToLowerInvariant().Contains(term));
        }

        var results = await query
            .OrderByDescending(x => x.Enrollment.EnrolledAt)
            .ToListAsync();

        // Build DTOs with org names
        var orgCache = new Dictionary<Guid, string>();
        var dtos = new List<EnrollmentDto>();

        foreach (var r in results)
        {
            var orgId = r.Student.OrganizationId;
            if (!orgCache.TryGetValue(orgId, out var orgName))
            {
                var org = await managementCtx.Organizations
                    .Where(o => o.Id == orgId && !o.IsDeleted)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync();
                orgName = org ?? "Unknown";
                orgCache[orgId] = orgName;
            }

            dtos.Add(new EnrollmentDto(
                r.Enrollment.Id,
                r.Student.Id,
                r.Student.Name,
                r.Student.Email,
                r.Course.Id,
                r.Course.Title,
                orgName,
                r.Enrollment.EnrolledAt
            ));
        }

        return dtos;
    }

    /// <summary>List all enrollments (SuperUser).</summary>
    public async Task<IList<EnrollmentDto>> ListAllEnrollmentsAsync(string? studentName = null, string? courseTitle = null)
    {
        var query = enrollmentCtx.Enrollments
            .Join(
                enrollmentCtx.Students,
                e => e.StudentId,
                s => s.Id,
                (e, s) => new { Enrollment = e, Student = s })
            .Join(
                catalogCtx.Courses,
                x => x.Enrollment.CourseId,
                c => c.Id,
                (x, c) => new { x.Enrollment, x.Student, Course = c });

        if (!string.IsNullOrWhiteSpace(studentName))
        {
            var term = studentName.ToLowerInvariant();
            query = query.Where(x => x.Student.Name.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(courseTitle))
        {
            var term = courseTitle.ToLowerInvariant();
            query = query.Where(x => x.Course.Title.ToLowerInvariant().Contains(term));
        }

        var results = await query
            .OrderByDescending(x => x.Enrollment.EnrolledAt)
            .ToListAsync();

        var orgCache = new Dictionary<Guid, string>();
        var dtos = new List<EnrollmentDto>();

        foreach (var r in results)
        {
            var orgId = r.Student.OrganizationId;
            if (!orgCache.TryGetValue(orgId, out var orgName))
            {
                var org = await managementCtx.Organizations
                    .Where(o => o.Id == orgId && !o.IsDeleted)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync();
                orgName = org ?? "Unknown";
                orgCache[orgId] = orgName;
            }

            dtos.Add(new EnrollmentDto(
                r.Enrollment.Id,
                r.Student.Id,
                r.Student.Name,
                r.Student.Email,
                r.Course.Id,
                r.Course.Title,
                orgName,
                r.Enrollment.EnrolledAt
            ));
        }

        return dtos;
    }
}
