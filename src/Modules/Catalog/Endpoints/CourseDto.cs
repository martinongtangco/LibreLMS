namespace LearningLms.Modules.Catalog.Endpoints;

/// <summary>DTO for catalog listing — excludes full description for brevity.</summary>
public record CourseDto(
    Guid Id,
    string Title,
    string ShortDescription,
    string Category,
    string Duration
);
