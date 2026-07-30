using System.Text.Json.Serialization;

namespace LibreLms.Modules.Catalog.Endpoints;

/// <summary>Full course details with optional enrollment status for the current student.</summary>
public record CourseDetailDto(
    Guid Id,
    string Title,
    string ShortDescription,
    string FullDescription,
    string Category,
    string Duration,
    [property: JsonPropertyName("isEnrolled")] bool IsEnrolled = false
);
