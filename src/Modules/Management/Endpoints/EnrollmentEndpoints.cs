namespace LibreLms.Modules.Management.Endpoints;

/// <summary>Request body for creating a single enrollment.</summary>
public record CreateEnrollmentRequest(Guid StudentId, Guid CourseId);

/// <summary>Request body for bulk enrollment.</summary>
public record BulkEnrollmentRequest(IList<Guid> StudentIds, Guid CourseId);

/// <summary>
/// Marker for the Management module's enrollment endpoint definitions.
/// Actual endpoint mapping is done in Program.cs (Host project).
/// </summary>
public static class EnrollmentEndpoints
{
}
