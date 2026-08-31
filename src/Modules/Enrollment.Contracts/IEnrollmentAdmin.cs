namespace LibreLms.Contracts.Enrollment;

/// <summary>Admin operations on enrollments, with existence checks, for other modules.</summary>
public interface IEnrollmentAdmin
{
    /// <summary>Enroll a student in a course. Throws KeyNotFoundException if the student or
    /// course does not exist. Returns AlreadyEnrolled=true (EnrollmentId=Guid.Empty) when the
    /// student is already enrolled — no exception for duplicates.</summary>
    Task<AdminEnrollResult> EnrollAsync(Guid studentId, Guid courseId);

    /// <summary>Enroll many students in a course. Throws KeyNotFoundException if the course
    /// does not exist. Students that do not exist are skipped (no result row); already-enrolled
    /// students return AlreadyEnrolled=true.</summary>
    Task<IList<AdminEnrollResult>> EnrollManyAsync(Guid courseId, IEnumerable<Guid> studentIds);

    /// <summary>Remove an enrollment. Returns false when the enrollment does not exist.</summary>
    Task<bool> UnenrollAsync(Guid enrollmentId);

    /// <summary>A student's enrollments with course titles (courses that no longer exist are omitted).</summary>
    Task<IList<AdminEnrollmentInfo>> GetStudentEnrollmentsAsync(Guid studentId);

    /// <summary>Enrollment count, optionally scoped to one organization (by the student's org).</summary>
    Task<int> CountEnrollmentsAsync(Guid? organizationId = null);

    /// <summary>Bulk variant of <see cref="CountEnrollmentsAsync"/>: total enrollment count across the
    /// given organizations (by the student's org, same join semantics as the per-org call, in one
    /// query). Equals the sum of the per-org calls. Empty input yields 0 without hitting the database.</summary>
    Task<int> CountEnrollmentsByOrgsAsync(IEnumerable<Guid> organizationIds);

    /// <summary>Most recent enrollments with learner info (courses that no longer exist are omitted).</summary>
    Task<IList<RecentEnrollmentInfo>> GetRecentEnrollmentsAsync(int take);

    /// <summary>All enrollments newest-first, optionally filtered by (case-insensitive) student
    /// name and course title. Enrollments whose course no longer exists are omitted.</summary>
    Task<IList<AdminEnrollmentInfo>> ListAsync(string? studentName = null, string? courseTitle = null);

    /// <summary>Paged admin listing, newest-first. Filters are case-insensitive contains on
    /// student name and course title. Enrollments whose course no longer exists are omitted
    /// (same semantics as ListAsync). Returns only the requested page plus the filtered total.</summary>
    Task<AdminEnrollmentPageResult> ListPagedAsync(
        string? studentName, string? courseTitle, int pageNumber, int pageSize);
}

/// <summary>Result of an enrollment operation. AlreadyEnrolled results carry EnrollmentId=Guid.Empty.</summary>
public record AdminEnrollResult(Guid EnrollmentId, Guid StudentId, Guid CourseId,
    bool AlreadyEnrolled, DateTimeOffset EnrolledAt);

/// <summary>One enrollment with its course title (no learner name — use IUserLookup for that).</summary>
public record AdminEnrollmentInfo(Guid EnrollmentId, Guid StudentId, Guid CourseId,
    DateTimeOffset EnrolledAt, string CourseTitle);

/// <summary>Recent enrollment with learner + course display data.</summary>
public record RecentEnrollmentInfo(Guid EnrollmentId, Guid StudentId, string StudentName,
    string StudentEmail, Guid CourseId, string CourseTitle, DateTimeOffset EnrolledAt);

/// <summary>One enrollment row as returned by the AdminListEnrollments procedure
/// (includes learner name/email and the learner's org id for display enrichment).</summary>
public record AdminEnrollmentRow(
    Guid EnrollmentId, Guid StudentId, string StudentName, string StudentEmail,
    Guid CourseId, string CourseTitle, Guid OrganizationId, DateTimeOffset EnrolledAt);

/// <summary>A page of admin enrollment rows plus the filtered total count.</summary>
public record AdminEnrollmentPageResult(IList<AdminEnrollmentRow> Items, int TotalCount);
