using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;

namespace LibreLms.Modules.Scorm.Application;

/// <summary>
/// Service for managing SCORM attempt records and student-facing queries.
/// </summary>
public class ScormAttemptService
{
    private readonly ScormDbContext _context;
    private readonly ICourseLookup _courseLookup;

    public ScormAttemptService(ScormDbContext context, ICourseLookup courseLookup)
    {
        _context = context;
        _courseLookup = courseLookup;
    }

    /// <summary>
    /// Get all attempts for a student, enriched with course titles.
    /// </summary>
    public async Task<IEnumerable<AttemptSummary>> GetMyAttemptsAsync(Guid studentId)
    {
        var attempts = await _context.CourseAttempts
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.LastCommitAt)
            .ToListAsync();

        // One batched cross-module lookup for all attempted courses (spec 048 E5) —
        // missing courses are simply absent from the map ("Unknown Course" fallback below).
        var courseIds = attempts.Select(a => a.CourseId).Distinct().ToList();
        var courseMap = new Dictionary<Guid, CourseSummary>();
        if (courseIds.Count > 0)
        {
            var courses = await _courseLookup.GetCoursesAsync(courseIds);
            courseMap = courses.ToDictionary(c => c.Id);
        }

        var summaries = new List<AttemptSummary>();

        foreach (var attempt in attempts)
        {
            courseMap.TryGetValue(attempt.CourseId, out var course);
            summaries.Add(new AttemptSummary(
                Id: attempt.Id,
                CourseId: attempt.CourseId,
                CourseTitle: course?.Title ?? "Unknown Course",
                AttemptNumber: attempt.AttemptNumber,
                Status: attempt.Status,
                ScoreRaw: attempt.ScoreRaw,
                SessionTime: attempt.SessionTime,
                StartedAt: attempt.StartedAt,
                CompletedAt: attempt.CompletedAt,
                LastCommitAt: attempt.LastCommitAt));
        }

        return summaries;
    }
}

/// <summary>Summary of a course attempt for API responses.</summary>
public record AttemptSummary(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    int AttemptNumber,
    string Status,
    double? ScoreRaw,
    string? SessionTime,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset LastCommitAt);
