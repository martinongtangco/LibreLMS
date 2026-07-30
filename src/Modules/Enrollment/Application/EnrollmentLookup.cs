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
}
