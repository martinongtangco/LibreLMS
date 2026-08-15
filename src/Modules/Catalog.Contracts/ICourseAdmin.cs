namespace LibreLms.Contracts.Catalog;

/// <summary>Catalog mutations exposed across the module boundary.</summary>
public interface ICourseAdmin
{
    /// <summary>Delete a course. Throws KeyNotFoundException when the course does not exist.</summary>
    Task<bool> DeleteAsync(Guid courseId);
}
