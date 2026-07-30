using System.Text.Json.Serialization;

namespace LibreLms.Modules.Enrollment.Endpoints;

/// <summary>Request body for enrolling in a course.</summary>
public record EnrollRequest(
    [property: JsonPropertyName("courseId")] Guid CourseId
);
