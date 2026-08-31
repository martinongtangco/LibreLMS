using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// Admin enrollment operations with existence checks (spec 027). Uses ICourseLookup for
/// everything Catalog-owned so the module boundary stays compiled (Constitution III).
/// </summary>
public sealed class EnrollmentAdminService : IEnrollmentAdmin
{
    private readonly EnrollmentDbContext _context;
    private readonly ICourseLookup _courseLookup;

    public EnrollmentAdminService(EnrollmentDbContext context, ICourseLookup courseLookup)
    {
        _context = context;
        _courseLookup = courseLookup;
    }

    public async Task<AdminEnrollResult> EnrollAsync(Guid studentId, Guid courseId)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("Student not found.");

        var course = await _courseLookup.GetCourseAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException("Course not found.");

        var alreadyEnrolled = await _context.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
        if (alreadyEnrolled)
            return new AdminEnrollResult(Guid.Empty, studentId, courseId, AlreadyEnrolled: true, EnrolledAt: DateTimeOffset.UtcNow);

        var enrollment = new LibreLms.Modules.Enrollment.Domain.Enrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            EnrolledAt = DateTimeOffset.UtcNow
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        return new AdminEnrollResult(enrollment.Id, studentId, courseId, AlreadyEnrolled: false, enrollment.EnrolledAt);
    }

    public async Task<IList<AdminEnrollResult>> EnrollManyAsync(Guid courseId, IEnumerable<Guid> studentIds)
    {
        var course = await _courseLookup.GetCourseAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException("Course not found.");

        var results = new List<AdminEnrollResult>();
        var created = 0;

        foreach (var studentId in studentIds)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student is null)
                continue; // missing students are skipped (no result row)

            var alreadyEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
            if (alreadyEnrolled)
            {
                results.Add(new AdminEnrollResult(Guid.Empty, studentId, courseId, AlreadyEnrolled: true, EnrolledAt: DateTimeOffset.UtcNow));
                continue;
            }

            var enrollment = new LibreLms.Modules.Enrollment.Domain.Enrollment
            {
                StudentId = studentId,
                CourseId = courseId,
                EnrolledAt = DateTimeOffset.UtcNow
            };
            _context.Enrollments.Add(enrollment);
            created++;
            results.Add(new AdminEnrollResult(enrollment.Id, studentId, courseId, AlreadyEnrolled: false, enrollment.EnrolledAt));
        }

        if (created > 0)
            await _context.SaveChangesAsync();

        return results;
    }

    public async Task<bool> UnenrollAsync(Guid enrollmentId)
    {
        var enrollment = await _context.Enrollments.FindAsync(enrollmentId);
        if (enrollment is null)
            return false;

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IList<AdminEnrollmentInfo>> GetStudentEnrollmentsAsync(Guid studentId)
    {
        var rows = await _context.Enrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => new { e.Id, e.StudentId, e.CourseId, e.EnrolledAt })
            .ToListAsync();

        var courses = await GetCourseMapAsync(rows.Select(x => x.CourseId));
        return rows
            .Where(x => courses.ContainsKey(x.CourseId))
            .Select(x => new AdminEnrollmentInfo(x.Id, x.StudentId, x.CourseId, x.EnrolledAt, courses[x.CourseId].Title))
            .ToList();
    }

    public async Task<int> CountEnrollmentsAsync(Guid? organizationId = null)
    {
        return organizationId is null
            ? await _context.Enrollments.CountAsync()
            : await _context.Enrollments
                .Join(_context.Students, e => e.StudentId, s => s.Id, (e, s) => new { e.Id, s.OrganizationId })
                .CountAsync(x => x.OrganizationId == organizationId.Value);
    }

    /// <summary>
    /// Bulk variant of <see cref="CountEnrollmentsAsync"/> — same join semantics (Enrollments
    /// joined to Students, scoped by the STUDENT's organization), collapsed to one query with
    /// an IN filter. The result equals the sum of the per-org calls.
    /// </summary>
    public async Task<int> CountEnrollmentsByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var orgIds = organizationIds.Distinct().ToList();
        if (orgIds.Count == 0)
            return 0;

        return await _context.Enrollments
            .Join(_context.Students, e => e.StudentId, s => s.Id, (e, s) => new { e.Id, s.OrganizationId })
            .CountAsync(x => orgIds.Contains(x.OrganizationId));
    }

    public async Task<IList<RecentEnrollmentInfo>> GetRecentEnrollmentsAsync(int take)
    {
        var rows = await _context.Enrollments
            .Join(_context.Students, e => e.StudentId, s => s.Id, (e, s) => new
            {
                e.Id,
                e.CourseId,
                e.EnrolledAt,
                StudentId = s.Id,
                s.Name,
                s.Email
            })
            .OrderByDescending(x => x.EnrolledAt)
            .Take(take)
            .ToListAsync();

        var courses = await GetCourseMapAsync(rows.Select(x => x.CourseId));
        return rows
            .Where(x => courses.ContainsKey(x.CourseId))
            .Select(x => new RecentEnrollmentInfo(x.Id, x.StudentId, x.Name, x.Email, x.CourseId, courses[x.CourseId].Title, x.EnrolledAt))
            .ToList();
    }

    public async Task<IList<AdminEnrollmentInfo>> ListAsync(string? studentName = null, string? courseTitle = null)
    {
        var query = _context.Enrollments
            .Join(_context.Students, e => e.StudentId, s => s.Id, (e, s) => new
            {
                e.Id,
                e.CourseId,
                e.EnrolledAt,
                StudentId = s.Id,
                s.Name
            });

        if (!string.IsNullOrWhiteSpace(studentName))
        {
            var term = studentName.ToLowerInvariant();
            query = query.Where(x => x.Name.ToLowerInvariant().Contains(term));
        }

        var rows = await query
            .OrderByDescending(x => x.EnrolledAt)
            .ToListAsync();

        var courses = await GetCourseMapAsync(rows.Select(x => x.CourseId));
        var result = rows
            .Where(x => courses.ContainsKey(x.CourseId))
            .Select(x => new AdminEnrollmentInfo(x.Id, x.StudentId, x.CourseId, x.EnrolledAt, courses[x.CourseId].Title))
            .ToList();

        if (!string.IsNullOrWhiteSpace(courseTitle))
        {
            var term = courseTitle.ToLowerInvariant();
            result = result.Where(r => r.CourseTitle.ToLowerInvariant().Contains(term)).ToList();
        }

        return result;
    }

    /// <summary>
    /// Paged admin listing, newest-first, via the AdminListEnrollments stored procedure
    /// (created by an EF migration). Filters are case-insensitive contains on student name
    /// and course title; whitespace-only filters are sent as NULL (no filter).
    /// Rows whose course no longer exists are omitted (same semantics as ListAsync).
    /// Returns the requested page plus the filtered total count.
    /// </summary>
    public async Task<AdminEnrollmentPageResult> ListPagedAsync(
        string? studentName, string? courseTitle, int pageNumber, int pageSize)
    {
        studentName = studentName?.Trim();
        if (string.IsNullOrWhiteSpace(studentName))
            studentName = null;

        courseTitle = courseTitle?.Trim();
        if (string.IsNullOrWhiteSpace(courseTitle))
            courseTitle = null;

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        try
        {
            using var command = new SqlCommand("AdminListEnrollments", (SqlConnection)connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("@StudentName", SqlDbType.NVarChar, 200).Value = studentName ?? (object)DBNull.Value;
            command.Parameters.Add("@CourseTitle", SqlDbType.NVarChar, 200).Value = courseTitle ?? (object)DBNull.Value;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;

            var items = new List<AdminEnrollmentRow>();
            var totalCount = 0;

            using var reader = await command.ExecuteReaderAsync();

            // Result Set 1: enrollment rows
            while (reader.Read())
            {
                items.Add(new AdminEnrollmentRow(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetGuid(4),
                    reader.GetString(5),
                    reader.GetGuid(6),
                    reader.GetDateTimeOffset(7)
                ));
            }

            // Move to Result Set 2: filtered total count
            await reader.NextResultAsync();
            if (reader.Read())
            {
                totalCount = reader.GetInt32(0);
            }

            return new AdminEnrollmentPageResult(items, totalCount);
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    /// <summary>Batch course lookup by id — the cross-module replacement for joining the Catalog DbContext.</summary>
    private async Task<Dictionary<Guid, CourseSummary>> GetCourseMapAsync(IEnumerable<Guid> courseIds)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, CourseSummary>();

        var courses = await _courseLookup.GetCoursesAsync(ids);
        return courses.ToDictionary(c => c.Id);
    }
}
