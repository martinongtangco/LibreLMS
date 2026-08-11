namespace LibreLms.Modules.Catalog.Endpoints;

/// <summary>Request body for updating an existing course.</summary>
public record UpdateCourseRequest(
    string Title,
    string ShortDescription,
    string FullDescription,
    string Category,
    string Duration
);
