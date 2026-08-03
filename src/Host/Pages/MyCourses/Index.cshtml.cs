using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Scorm.Application;

namespace LibreLms.Host.Pages.MyCourses;

public class MyCoursesModel : PageModel
{
    private readonly EnrollmentService _enrollmentService;
    private readonly ScormAttemptService _scormAttemptService;

    public MyCoursesModel(
        EnrollmentService enrollmentService,
        ScormAttemptService scormAttemptService)
    {
        _enrollmentService = enrollmentService;
        _scormAttemptService = scormAttemptService;
    }

    public List<EnrollmentRow> EnrollmentRows { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            await LoadEnrollments();
        }
        catch
        {
            // Show empty state on failure
        }
    }

    /// <summary>HTMX handler: return enrollment list partial for inline refresh (US4).</summary>
    public async Task<PartialViewResult> OnGetEnrollmentsAsync()
    {
        try
        {
            var model = await BuildEnrollmentRows();
            return Partial("_EnrollmentList", model);
        }
        catch
        {
            return Partial("_ErrorPartial", "Unable to load enrollment data. Please refresh.");
        }
    }

    private async Task LoadEnrollments()
    {
        var rows = await BuildEnrollmentRows();
        EnrollmentRows = rows;
    }

    private async Task<List<EnrollmentRow>> BuildEnrollmentRows()
    {
        var studentId = ScormHelpers.GetStudentId(HttpContext);

        var enrollments = await _enrollmentService.GetMyEnrollmentsAsync(studentId);
        var attempts = await _scormAttemptService.GetMyAttemptsAsync(studentId);

        // Join enrollments with latest SCORM attempt per course
        var attemptByCourse = attempts
            .GroupBy(a => a.CourseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttemptNumber).First());

        return enrollments.Select(e =>
        {
            var attempt = attemptByCourse.TryGetValue(e.Enrollment.CourseId, out var a) ? a : null;
            var status = attempt?.Status;
            var score = attempt?.ScoreRaw;
            var displayLabel = ScormHelpers.GetDisplayLabel(status);
            var progressPercent = ComputeProgressPercent(status, score);
            var statusTagClass = string.IsNullOrEmpty(status) ? "tag-neutral" : "tag-accent-2";
            return new EnrollmentRow(
                EnrollmentId: e.Enrollment.Id,
                CourseId: e.Enrollment.CourseId,
                CourseTitle: e.CourseTitle,
                EnrolledAt: e.Enrollment.EnrolledAt,
                LatestStatus: status,
                LatestScore: score,
                StatusLabel: displayLabel,
                StatusTagClass: statusTagClass,
                ProgressPercent: progressPercent);
        }).ToList();
    }

    /// <summary>Derive a 0–100 progress percentage from SCORM status/score.</summary>
    private static int ComputeProgressPercent(string? status, double? score)
    {
        if (string.IsNullOrEmpty(status))
            return 0; // Not Started
        if (score.HasValue)
            return (int)score.Value;
        var lower = status.ToLowerInvariant();
        if (lower == "completed" || lower == "passed")
            return 100;
        return 0;
    }
}

/// <summary>View model for enrollment row partial view (HTMX swaps).</summary>
public record EnrollmentRow(
    Guid EnrollmentId,
    Guid CourseId,
    string CourseTitle,
    DateTimeOffset EnrolledAt,
    string? LatestStatus,
    double? LatestScore,
    string StatusLabel = "",
    string StatusTagClass = "",
    int ProgressPercent = 0);
