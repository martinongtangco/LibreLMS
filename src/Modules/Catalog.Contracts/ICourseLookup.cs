namespace LibreLms.Contracts.Catalog;

/// <summary>
/// Cross-module contract for looking up course existence and metadata.
/// The Enrollment module uses this to validate a course before creating an enrollment.
/// Implemented by the Catalog module and registered in DI.
/// </summary>
public interface ICourseLookup
{
    /// <summary>Get one course, or null if it does not exist.</summary>
    Task<CourseSummary?> GetCourseAsync(Guid courseId);

    /// <summary>Total course count in the catalog.</summary>
    Task<int> CountAsync();

    /// <summary>Course count owned by one organization.</summary>
    Task<int> CountByOrgAsync(Guid organizationId);

    /// <summary>Bulk variant of <see cref="CountByOrgAsync"/>: course count per organization in a
    /// single GROUP BY query. Organizations with zero courses are absent from the result.
    /// Empty input yields an empty dictionary without hitting the database.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetCourseCountsByOrgsAsync(IEnumerable<Guid> organizationIds);

    /// <summary>Distinct category names across the whole catalog, ordered ascending.</summary>
    Task<IList<string>> GetDistinctCategoriesAsync();

    /// <summary>Batch lookup of courses by id (missing ids are simply absent from the result).</summary>
    Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds);

    /// <summary>All courses owned by any of the given organizations.</summary>
    Task<IList<CourseSummary>> ListByOrgsAsync(IEnumerable<Guid> organizationIds);

    /// <summary>Every course in the catalog.</summary>
    Task<IList<CourseSummary>> ListAllAsync();
}
