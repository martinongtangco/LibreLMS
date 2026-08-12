namespace LibreLms.Modules.Catalog.Endpoints;

/// <summary>Request body for creating a new course.</summary>
public record CreateCourseRequest(
    string Title,
    string ShortDescription,
    string FullDescription,
    string Category,
    string Duration,
    Guid? OrganizationId,
    Guid? ScormPackageId = null
);
