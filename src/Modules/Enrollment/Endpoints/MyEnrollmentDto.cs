using System.Text.Json.Serialization;

namespace LearningLms.Modules.Enrollment.Endpoints;

/// <summary>DTO for an enrollment in the "My Courses" listing.</summary>
public record MyEnrollmentDto(
    Guid Id,
    [property: JsonPropertyName("courseId")] Guid CourseId,
    [property: JsonPropertyName("courseTitle")] string CourseTitle,
    [property: JsonPropertyName("enrolledAt")] DateTimeOffset EnrolledAt
);
