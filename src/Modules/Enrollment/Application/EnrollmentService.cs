using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>Application service for enrollment operations.</summary>
public class EnrollmentService
{
    private readonly EnrollmentDbContext _context;
    private readonly ICourseLookup _courseLookup;

    public EnrollmentService(EnrollmentDbContext context, ICourseLookup courseLookup)
    {
        _context = context;
        _courseLookup = courseLookup;
    }

    /// <summary>
    /// Enroll a student in a course.
    /// Returns the created Enrollment on success, null on duplicate.
    /// Throws if the course doesn't exist.
    /// </summary>
    public async Task<(LibreLms.Modules.Enrollment.Domain.Enrollment? Enrollment, bool IsDuplicate, bool CourseNotFound)> EnrollAsync(Guid studentId, Guid courseId)
    {
        // Validate course exists via cross-module contract
        var courseSummary = await _courseLookup.GetCourseAsync(courseId);
        if (courseSummary is null)
            return (null, false, true);

        // Check for duplicate enrollment
        var existing = await _context.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);

        if (existing)
            return (null, true, false);

        // Create enrollment
        var enrollment = new LibreLms.Modules.Enrollment.Domain.Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            EnrolledAt = DateTimeOffset.UtcNow
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        return (enrollment, false, false);
    }

    /// <summary>Get all enrollments for a student with course titles.</summary>
    public async Task<IEnumerable<(LibreLms.Modules.Enrollment.Domain.Enrollment Enrollment, string CourseTitle)>> GetMyEnrollmentsAsync(Guid studentId)
    {
        var enrollments = await _context.Enrollments
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var results = new List<(LibreLms.Modules.Enrollment.Domain.Enrollment, string)>();
        foreach (var enrollment in enrollments)
        {
            var summary = await _courseLookup.GetCourseAsync(enrollment.CourseId);
            results.Add((enrollment, summary?.Title ?? "Unknown Course"));
        }

        return results;
    }
}
