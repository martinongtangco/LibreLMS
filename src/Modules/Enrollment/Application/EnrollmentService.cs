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

        // One batched cross-module lookup for all enrolled courses (spec 048 E4) —
        // missing courses are simply absent from the map ("Unknown Course" fallback below).
        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
        var courseMap = new Dictionary<Guid, CourseSummary>();
        if (courseIds.Count > 0)
        {
            var summaries = await _courseLookup.GetCoursesAsync(courseIds);
            courseMap = summaries.ToDictionary(c => c.Id);
        }

        var results = new List<(LibreLms.Modules.Enrollment.Domain.Enrollment, string)>();
        foreach (var enrollment in enrollments)
        {
            courseMap.TryGetValue(enrollment.CourseId, out var summary);
            results.Add((enrollment, summary?.Title ?? "Unknown Course"));
        }

        return results;
    }

    /// <summary>
    /// Get enrollment counts grouped by course ID.
    /// Returns only courses that have at least one enrollment.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> GetEnrollmentCountsByCourseAsync(IEnumerable<Guid> courseIds)
    {
        var counts = await _context.Enrollments
            .Where(e => courseIds.Contains(e.CourseId))
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(c => c.CourseId, c => c.Count);
    }

    /// <summary>Get the two Settings preferences for a student.</summary>
    public async Task<(bool EmailNotificationsEnabled, string ThemePreference)> GetPreferencesAsync(Guid studentId)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student is null)
            return (true, "System");
        return (student.EmailNotificationsEnabled, student.ThemePreference);
    }

    /// <summary>Update the two Settings preferences for a student.</summary>
    public async Task UpdatePreferencesAsync(Guid studentId, bool emailNotificationsEnabled, string themePreference)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student is null)
            return;
        student.EmailNotificationsEnabled = emailNotificationsEnabled;
        student.ThemePreference = themePreference;
        await _context.SaveChangesAsync();
    }
}
