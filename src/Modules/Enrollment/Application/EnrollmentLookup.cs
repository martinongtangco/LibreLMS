using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// Implements the cross-module IEnrollmentLookup contract.
/// Queries EnrollmentDbContext.Enrollments to check enrollment status.
/// </summary>
public class EnrollmentLookup(EnrollmentDbContext context) : IEnrollmentLookup
{
    public async Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId)
    {
        return await context.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
    }

    public async Task<IReadOnlyCollection<Guid>> GetEnrolledCourseIdsAsync(Guid studentId, IEnumerable<Guid> courseIds)
    {
        var ids = courseIds.ToList();
        if (ids.Count == 0)
            return Array.Empty<Guid>();

        // One query: WHERE StudentId = @s AND CourseId IN @ids (unique (StudentId, CourseId) index).
        return await context.Enrollments
            .Where(e => e.StudentId == studentId && ids.Contains(e.CourseId))
            .Select(e => e.CourseId)
            .ToListAsync();
    }
}
