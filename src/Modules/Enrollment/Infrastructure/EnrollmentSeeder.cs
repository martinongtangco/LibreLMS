using LearningLms.Modules.Enrollment.Domain;
using LearningLms.Modules.Enrollment.Infrastructure;

namespace LearningLms.Modules.Enrollment.Infrastructure;

/// <summary>Seeds test students with known credentials for demonstration.</summary>
public static class EnrollmentSeeder
{
    public static void Seed(EnrollmentDbContext context)
    {
        var students = new[]
        {
            new Student
            {
                Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
                Name = "Alice Johnson",
                Email = "alice@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Student
            {
                Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440002"),
                Name = "Bob Smith",
                Email = "bob@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Student
            {
                Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440003"),
                Name = "Carol Davis",
                Email = "carol@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        context.Students.AddRange(students);
        context.SaveChanges();
    }
}
