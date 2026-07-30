namespace LibreLms.Contracts.Catalog;

/// <summary>
/// Cross-module contract for looking up course existence and metadata.
/// The Enrollment module uses this to validate a course before creating an enrollment.
/// Implemented by the Catalog module and registered in DI.
/// </summary>
public interface ICourseLookup
{
    Task<CourseSummary?> GetCourseAsync(Guid courseId);
}
