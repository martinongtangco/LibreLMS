namespace LibreLms.Contracts.Scorm;

/// <summary>
/// Aggregate over a set of SCORM attempts (all platform attempts, or the attempts of a
/// given student set). Derived on read, never stored (spec 058 US1 — dashboards stop
/// hardcoding 0 and compute real aggregates over CourseAttempt data).
/// </summary>
public record AttemptStatsSummary(
    /// <summary>Number of attempts in the set.</summary>
    int TotalAttempts,
    /// <summary>
    /// Attempts whose status is a terminal SCORM completion state
    /// ("completed", "passed", "failed" — case-insensitive; the SCORM shim validates
    /// lesson_status case-insensitively, so stored values may vary in case).
    /// </summary>
    int CompletedAttempts,
    /// <summary>Completed attempts that have a recorded score (ScoreRaw != null).</summary>
    int ScoredCompletedAttempts,
    /// <summary>Sum of ScoreRaw over the scored completed attempts.</summary>
    double CompletedScoreSum,
    /// <summary>Distinct courses with at least one completed attempt in the set.</summary>
    int DistinctCompletedCourses);

/// <summary>
/// Cross-module contract for SCORM attempt aggregates. Implemented by the Scorm module,
/// consumed by Management's dashboard (spec 058 US1). The Scorm module owns what "completed"
/// means (the terminal lesson-status set); callers scope by student set — the Scorm module
/// knows nothing about organizations.
/// </summary>
public interface IScormAttemptStats
{
    /// <summary>Aggregate over every attempt on the platform (System dashboard).</summary>
    Task<AttemptStatsSummary> GetSystemStatsAsync();

    /// <summary>
    /// Aggregate over the attempts made by the given students (Org/Personal dashboards).
    /// Empty input yields the all-zero summary without touching the database.
    /// </summary>
    Task<AttemptStatsSummary> GetStatsForStudentsAsync(IEnumerable<Guid> studentIds);
}
