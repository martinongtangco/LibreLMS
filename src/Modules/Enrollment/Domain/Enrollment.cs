using LearningLms.SharedKernel;

namespace LearningLms.Modules.Enrollment.Domain;

/// <summary>Represents the relationship between a Student and a Course.</summary>
public class Enrollment : Entity<Guid>
{
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public DateTimeOffset EnrolledAt { get; set; }

    public Enrollment()
    {
        Id = Guid.NewGuid();
        EnrolledAt = DateTimeOffset.UtcNow;
    }
}
