namespace LibreLms.Modules.Management.Endpoints;

/// <summary>DTO for visibility override settings.</summary>
public record SetVisibilityRequest(Guid OrganizationId, bool IsHidden, Guid? CreatedBy);

/// <summary>
/// Marker for the Management module's course management endpoint definitions.
/// Actual endpoint mapping is done in Program.cs (Host project).
/// </summary>
public static class CourseManagementEndpoints
{
}
