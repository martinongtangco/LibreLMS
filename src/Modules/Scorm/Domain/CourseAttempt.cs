using LearningLms.SharedKernel;

namespace LearningLms.Modules.Scorm.Domain;

/// <summary>
/// Represents a single student's attempt at a SCORM course. Multiple attempts
/// per student/course are allowed (retakes).
/// </summary>
public class CourseAttempt : Entity<Guid>
{
    /// <summary>The student who made this attempt.</summary>
    public Guid StudentId { get; set; }

    /// <summary>The course attempted.</summary>
    public Guid CourseId { get; set; }

    /// <summary>Sequential attempt number per student/course (1, 2, 3...).</summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>One of: "in-progress", "completed", "abandoned", "passed", "failed".</summary>
    public string Status { get; set; } = "in-progress";

    /// <summary>Raw score from cmi.core.score.raw (0–100, null if not set).</summary>
    public double? ScoreRaw { get; set; }

    /// <summary>Cumulative session time in "HH:MM:SS" format.</summary>
    public string? SessionTime { get; set; }

    /// <summary>Last committed cmi.suspend_data for resume (up to 64KB).</summary>
    public string? SuspendData { get; set; }

    /// <summary>When the attempt session began.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the attempt was completed/finished (set on LMSFinish).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Timestamp of the last LMSCommit or LMSFinish.</summary>
    public DateTimeOffset LastCommitAt { get; set; }

    public CourseAttempt()
    {
        Id = Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
        LastCommitAt = DateTimeOffset.UtcNow;
    }
}
