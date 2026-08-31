using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;

namespace LibreLms.Modules.Scorm.Application;

/// <summary>
/// Manages the full SCORM session lifecycle: launch, setValue, getValue, commit, finish.
/// Coordinates between Valkey (live state) and MSSQL (durable records).
/// </summary>
public class ScormSessionService
{
    private readonly ScormDbContext _scormContext;
    private readonly IScormSessionStore _sessionStore;
    private readonly IEnrollmentLookup _enrollmentLookup;
    private readonly ScormPackageService _packageService;

    public ScormSessionService(
        ScormDbContext scormContext,
        IScormSessionStore sessionStore,
        IEnrollmentLookup enrollmentLookup,
        ScormPackageService packageService)
    {
        _scormContext = scormContext;
        _sessionStore = sessionStore;
        _enrollmentLookup = enrollmentLookup;
        _packageService = packageService;
    }

    /// <summary>Valid SCORM 1.2 lesson_status values.</summary>
    private static readonly HashSet<string> ValidLessonStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "not attempted", "incomplete", "completed", "passed", "failed", "browsed", "neutral"
    };

    /// <summary>
    /// Launch a SCORM session for a student/course pair.
    /// Validates enrollment, checks for active session, creates Valkey session + CourseAttempt.
    /// </summary>
    public async Task<LaunchResult> LaunchAsync(Guid studentId, Guid courseId)
    {
        // Validate enrollment (stable across retries — run once)
        var isEnrolled = await _enrollmentLookup.IsEnrolledAsync(studentId, courseId);
        if (!isEnrolled)
            return LaunchResult.CreateNotEnrolled();

        // Attempt numbers are max+1, and that read-then-insert is not atomic:
        // two concurrent launches for the same student/course both pass the
        // active-session check, read the same max, and the unique index
        // IX_CourseAttempts_StudentId_CourseId_AttemptNumber rejects the second
        // insert (SQL 2601). Retry with a fresh read — the losing insert is
        // rolled back, so the next try sees the new max (bug-044).
        //
        // bug-046: the duplicate key surfaces as a DbUpdateException wrapping
        // the SqlException — EF Core never lets the raw SqlException escape
        // SaveChangesAsync, so catching SqlException here (as spec 044 did)
        // was dead code and the race still 500'd. Catch the wrapper and
        // inspect the inner SqlException's number.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TryLaunchCoreAsync(studentId, courseId);
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
            {
                if (attempt >= 3)
                    return LaunchResult.CreateConflict();
                _scormContext.ChangeTracker.Clear(); // drop the failed Added entity
                await Task.Delay(50 * attempt);
            }
        }
    }

    /// <summary>
    /// One launch attempt: active-session check, attempt numbering (max+1),
    /// CourseAttempt insert, Valkey session. Retried by LaunchAsync on the
    /// duplicate-key conflict described there (bug-044).
    /// </summary>
    private async Task<LaunchResult> TryLaunchCoreAsync(Guid studentId, Guid courseId)
    {
        // Check for active session (concurrent session prevention)
        var existingKey = await _sessionStore.FindActiveSessionKeyAsync(studentId, courseId);
        if (!string.IsNullOrEmpty(existingKey))
            return LaunchResult.CreateSessionAlreadyActive();

        // Determine entry mode and attempt number
        var lastAttempt = await _scormContext.CourseAttempts
            .Where(a => a.StudentId == studentId && a.CourseId == courseId)
            .OrderByDescending(a => a.AttemptNumber)
            .FirstOrDefaultAsync();

        var attemptNumber = (lastAttempt?.AttemptNumber ?? 0) + 1;
        var entryMode = "initial";
        var suspendData = "";

        // If there's an incomplete previous attempt, resume mode
        if (lastAttempt is not null && lastAttempt.Status == "in-progress")
        {
            entryMode = "resume";
            suspendData = lastAttempt.SuspendData ?? "";
        }

        // Create CourseAttempt record
        var attempt = new CourseAttempt
        {
            StudentId = studentId,
            CourseId = courseId,
            AttemptNumber = attemptNumber,
            Status = "in-progress",
            StartedAt = DateTimeOffset.UtcNow,
            LastCommitAt = DateTimeOffset.UtcNow,
            SuspendData = suspendData
        };

        _scormContext.CourseAttempts.Add(attempt);
        await _scormContext.SaveChangesAsync();

        // Create Valkey session with default CMI values
        var sessionId = Guid.NewGuid();
        var sessionData = SessionData.CreateDefault(sessionId, studentId, courseId, attempt.Id, entryMode);

        // Restore suspend data for resume
        if (!string.IsNullOrEmpty(suspendData))
        {
            await _sessionStore.SetValueAsync(sessionId, "cmi.suspend_data", suspendData);
        }

        // Compute contentUrl from the SCORM package
        var contentUrl = await _packageService.FindLaunchPath(courseId);

        await _sessionStore.CreateSessionAsync(sessionData);

        return LaunchResult.CreateSuccess(sessionId.ToString(), entryMode, attemptNumber, contentUrl);
    }

    /// <summary>
    /// Set a CMI value in the current session. Validates the field and value.
    /// </summary>
    public async Task<SetValueResult> SetValueAsync(Guid sessionId, string element, string value)
    {
        // Validate the element is a known CMI field
        if (!IsValidCmiElement(element))
            return SetValueResult.CreateError("401", $"Unknown element: {element}");

        // Validate lesson_status values
        if (element == "cmi.core.lesson_status" && !ValidLessonStatuses.Contains(value))
            return SetValueResult.CreateError("403", $"The value specified for {element} is not valid. Must be one of: {string.Join(", ", ValidLessonStatuses)}");

        // Validate score range (0-100)
        if (element == "cmi.core.score.raw")
        {
            if (!double.TryParse(value, out var score) || score < 0 || score > 100)
                return SetValueResult.CreateError("403", $"The value specified for cmi.core.score.raw is out of range. Must be between 0 and 100.");
        }

        // Validate session_time format (HH:MM:SS)
        if (element == "cmi.core.session_time" && !IsValidSessionTime(value))
            return SetValueResult.CreateError("403", $"The value specified for cmi.core.session_time is not in a valid time format. Expected HH:MM:SS.");

        // Store in Valkey
        var success = await _sessionStore.SetValueAsync(sessionId, element, value);
        if (!success)
            return SetValueResult.CreateError("404", "Session not found or expired.");

        return SetValueResult.CreateSuccess();
    }

    /// <summary>
    /// Get a CMI value from the current session.
    /// </summary>
    public async Task<GetValueResult> GetValueAsync(Guid sessionId, string element)
    {
        var value = await _sessionStore.GetValueAsync(sessionId, element);
        if (value is null)
            return GetValueResult.CreateNotFound();

        return GetValueResult.CreateSuccess(value);
    }

    /// <summary>
    /// Commit session state to MSSQL (LMSCommit).
    /// Reads full CMI bag from Valkey, updates CourseAttempt in MSSQL.
    /// </summary>
    public async Task<CommitResult> CommitAsync(Guid sessionId)
    {
        var sessionData = await _sessionStore.ReadSessionAsync(sessionId);
        if (sessionData is null)
            return CommitResult.CreateNotFound();

        if (!Guid.TryParse(sessionData.AttemptId, out var attemptId))
            return CommitResult.CreateNotFound();

        var attempt = await _scormContext.CourseAttempts.FindAsync(attemptId);
        if (attempt is null)
            return CommitResult.CreateNotFound();

        // Update attempt from session state
        attempt.Status = sessionData.CmiLessonStatus;

        // Save score >= 0 (score of 0 is a legitimate SCORM score, e.g., for failed courses)
        if (double.TryParse(sessionData.CmiScoreRaw, out var score) && score >= 0)
            attempt.ScoreRaw = score;

        attempt.SessionTime = sessionData.CmiSessionTime;
        attempt.SuspendData = sessionData.CmiSuspendData;
        attempt.LastCommitAt = DateTimeOffset.UtcNow;

        await _scormContext.SaveChangesAsync();

        return CommitResult.CreateSuccess(attempt.LastCommitAt);
    }

    /// <summary>
    /// Finish the session (LMSFinish). Commits data, sets CompletedAt, deletes Valkey session.
    /// </summary>
    public async Task<FinishResult> FinishAsync(Guid sessionId, string exitReason = "normal")
    {
        var sessionData = await _sessionStore.ReadSessionAsync(sessionId);
        if (sessionData is null)
            return FinishResult.CreateNotFound();

        if (!Guid.TryParse(sessionData.AttemptId, out var attemptId))
            return FinishResult.CreateNotFound();

        var attempt = await _scormContext.CourseAttempts.FindAsync(attemptId);
        if (attempt is null)
            return FinishResult.CreateNotFound();

        // Update attempt with final state
        attempt.Status = sessionData.CmiLessonStatus;
        attempt.CompletedAt = DateTimeOffset.UtcNow;
        attempt.LastCommitAt = DateTimeOffset.UtcNow;

        // Save score >= 0 (score of 0 is a legitimate SCORM score, e.g., for failed courses)
        if (double.TryParse(sessionData.CmiScoreRaw, out var score) && score >= 0)
            attempt.ScoreRaw = score;

        attempt.SessionTime = sessionData.CmiSessionTime;
        attempt.SuspendData = sessionData.CmiSuspendData;

        await _scormContext.SaveChangesAsync();

        // Clean up Valkey session
        await _sessionStore.DeleteSessionAsync(sessionId);

        return FinishResult.CreateSuccess(attempt.Status, attempt.ScoreRaw);
    }

    /// <summary>
    /// True when the DbUpdateException wraps a SQL 2601 (unique-index violation)
    /// — EF Core's SaveChangesAsync always reports provider errors wrapped in
    /// DbUpdateException; the SqlException is the InnerException (verified by
    /// tests/Scorm.Tests DuplicateKeyExceptionContractTests).
    /// </summary>
    private static bool IsDuplicateKeyViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql && sql.Number == 2601;

    private static bool IsValidCmiElement(string element)
    {
        return element switch
        {
            "cmi.core.student_id" or
            "cmi.core.student_name" or
            "cmi.core.lesson_status" or
            "cmi.core.credit" or
            "cmi.core.entry" or
            "cmi.core.exit" or
            "cmi.core.score.raw" or
            "cmi.core.session_time" or
            "cmi.suspend_data" => true,
            _ => false
        };
    }

    private static bool IsValidSessionTime(string time)
    {
        // Accept HH:MM:SS or H:MM:SS format
        var parts = time.Split(':');
        if (parts.Length != 3)
            return false;

        return int.TryParse(parts[0], out _)
            && int.TryParse(parts[1], out var mins) && mins is >= 0 and < 60
            && int.TryParse(parts[2], out var secs) && secs is >= 0 and < 60;
    }
}

/// <summary>Result of launching a SCORM session.</summary>
public record LaunchResult
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public string? EntryMode { get; init; }
    public int? AttemptNumber { get; init; }
    public string? ContentUrl { get; init; }
    public string? Error { get; init; }

    public static LaunchResult CreateSuccess(string sessionId, string entryMode, int attemptNumber, string? contentUrl = null)
        => new() { Success = true, SessionId = sessionId, EntryMode = entryMode, AttemptNumber = attemptNumber, ContentUrl = contentUrl };

    public static LaunchResult CreateNotEnrolled()
        => new() { Success = false, Error = "Student is not enrolled in this course." };

    public static LaunchResult CreateSessionAlreadyActive()
        => new() { Success = false, Error = "A session for this course is already active. Please close it before launching again." };

    public static LaunchResult CreateConflict()
        => new() { Success = false, Error = "A momentary conflict occurred while launching (the course may be opening in another tab). Please try again." };
}

/// <summary>Result of setting a CMI value.</summary>
public record SetValueResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMsg { get; init; }

    public static SetValueResult CreateSuccess() => new() { Success = true };
    public static SetValueResult CreateError(string errorCode, string errorMsg) => new() { Success = false, ErrorCode = errorCode, ErrorMsg = errorMsg };
}

/// <summary>Result of getting a CMI value.</summary>
public record GetValueResult
{
    public bool Found { get; init; }
    public string? Value { get; init; }

    public static GetValueResult CreateSuccess(string value) => new() { Found = true, Value = value };
    public static GetValueResult CreateNotFound() => new() { Found = false };
}

/// <summary>Result of committing a session.</summary>
public record CommitResult
{
    public bool Success { get; init; }
    public DateTimeOffset? CommittedAt { get; init; }
    public string? Error { get; init; }

    public static CommitResult CreateSuccess(DateTimeOffset? committedAt = null) => new() { Success = true, CommittedAt = committedAt ?? DateTimeOffset.UtcNow };
    public static CommitResult CreateNotFound() => new() { Success = false, Error = "Session not found or expired." };
}

/// <summary>Result of finishing a session.</summary>
public record FinishResult
{
    public bool Success { get; init; }
    public string? Status { get; init; }
    public double? Score { get; init; }
    public string? Error { get; init; }

    public static FinishResult CreateSuccess(string? status = null, double? score = null) => new() { Success = true, Status = status ?? "completed", Score = score };
    public static FinishResult CreateNotFound() => new() { Success = false, Error = "Session not found or expired." };
}
