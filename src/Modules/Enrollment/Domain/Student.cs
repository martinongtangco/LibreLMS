using LearningLms.SharedKernel;

namespace LearningLms.Modules.Enrollment.Domain;

/// <summary>A learner on the platform.</summary>
public class Student : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Student()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
