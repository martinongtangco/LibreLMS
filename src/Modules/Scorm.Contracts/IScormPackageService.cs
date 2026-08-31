using System.IO;

namespace LibreLms.Contracts.Scorm;

/// <summary>
/// Contract for SCORM package operations.
/// Allows other modules to interact with SCORM packages without referencing
/// the Scorm module's internals (Constitution Principle III).
/// </summary>
public interface IScormPackageService
{
    /// <summary>Check if a course has an associated SCORM package.</summary>
    Task<bool> HasPackageAsync(Guid courseId);

    /// <summary>
    /// Bulk variant of <see cref="HasPackageAsync"/> (spec 048 E6): returns the subset of
    /// <paramref name="courseIds"/> that have an associated SCORM package, resolved with a
    /// single <c>WHERE CourseId IN @ids</c> query instead of one query per course.
    /// Empty input returns an empty result without hitting the database.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetCourseIdsWithPackagesAsync(IEnumerable<Guid> courseIds);

    /// <summary>Get the content directory path for a course's SCORM package, if any.</summary>
    Task<string?> GetContentDirectoryAsync(Guid courseId);

    /// <summary>Delete the SCORM package associated with a course (entity + content directory).</summary>
    Task DeletePackageForCourseAsync(Guid courseId);

    /// <summary>Delete a SCORM package by its ID (entity + content directory).</summary>
    Task DeleteAsync(Guid packageId);

    /// <summary>Upload a SCORM ZIP package.</summary>
    Task<(object? Package, string? Error)> UploadAsync(Stream zipStream, Guid? courseId);

    /// <summary>Associate an available SCORM package with a course.</summary>
    Task AssociateWithCourseAsync(Guid packageId, Guid courseId);

    /// <summary>Replace the SCORM package for a course.</summary>
    Task<(object? Package, string? Error)> ReplacePackageAsync(Guid courseId, Stream zipStream);

    /// <summary>Get the SCORM package for a course.</summary>
    Task<object?> GetPackageByCourseIdAsync(Guid courseId);

    /// <summary>List available (unassociated) SCORM packages.</summary>
    Task<IEnumerable<object>> ListAvailableAsync();
}
