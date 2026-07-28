using System.Text.Json.Serialization;

namespace LearningLms.Modules.Enrollment.Endpoints;

/// <summary>DTO representing an enrollment confirmation.</summary>
public record EnrollmentDto(
    Guid Id,
    [property: JsonPropertyName("studentId")] Guid StudentId,
    [property: JsonPropertyName("courseId")] Guid CourseId,
    [property: JsonPropertyName("enrolledAt")] DateTimeOffset EnrolledAt
);
