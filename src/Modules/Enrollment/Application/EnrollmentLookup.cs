using Microsoft.EntityFrameworkCore;
using LearningLms.Contracts.Enrollment;
using LearningLms.Modules.Enrollment.Infrastructure;

namespace LearningLms.Modules.Enrollment.Application;

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
