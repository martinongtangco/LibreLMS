namespace LibreLms.Contracts.Enrollment;

/// <summary>
/// Cross-module contract for checking student enrollment.
/// Used by other modules (e.g., Scorm) to validate that a student is enrolled in a course.
/// </summary>
public interface IEnrollmentLookup
{
    /// <summary>
    /// Check if a student is enrolled in a specific course.
    /// </summary>
    /// <param name="studentId">The student's unique identifier.</param>
    /// <param name="courseId">The course's unique identifier.</param>
    /// <returns>True if the student is enrolled, false otherwise.</returns>
    Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId);
}
