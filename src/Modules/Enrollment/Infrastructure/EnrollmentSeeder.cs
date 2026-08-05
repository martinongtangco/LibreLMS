using System.Security.Cryptography;
using System.Text;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.SharedKernel;

namespace LibreLms.Modules.Enrollment.Infrastructure;

/// <summary>Seeds test students with known credentials for demonstration.</summary>
public static class EnrollmentSeeder
{
    /// <summary>Root organization ID — must match the ID created by ManagementSeeder.</summary>
    private static readonly Guid RootOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Hash a password using SHA256 for seeding purposes.</summary>
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hash);
    }

    // Canonical seed data: email → expected role.
    // Always enforce these roles — never trust stale DB state.
    private static readonly Dictionary<string, string> SeedRoles = new()
    {
        { "alice@example.com", RoleNames.Learner },
        { "bob@example.com", RoleNames.Learner },
        { "carol@example.com", RoleNames.Learner },
        { "admin@example.com", RoleNames.OrgAdmin },
    };

    public static void Seed(EnrollmentDbContext context)
    {
        var passwordHash = HashPassword("password123");

        var students = new[]
        {
            new Student
            {
                Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
                Name = "Alice Johnson",
                Email = "alice@example.com",
                PasswordHash = passwordHash,
                Roles = RoleNames.Learner,
                OrganizationId = RootOrgId,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Student
            {
                Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440002"),
                Name = "Bob Smith",
                Email = "bob@example.com",
                PasswordHash = passwordHash,
                Roles = RoleNames.Learner,
                OrganizationId = RootOrgId,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Student
            {
                Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440003"),
                Name = "Carol Davis",
                Email = "carol@example.com",
                PasswordHash = passwordHash,
                Roles = RoleNames.Learner,
                OrganizationId = RootOrgId,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Student
            {
                Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440099"),
                Name = "Admin User",
                Email = "admin@example.com",
                PasswordHash = passwordHash,
                Roles = RoleNames.OrgAdmin,
                OrganizationId = RootOrgId,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        context.Students.AddRange(students);
        context.SaveChanges();

        // Enforce canonical roles on ALL seed students (fixes stale data from prior runs)
        foreach (var student in context.Students)
        {
            if (SeedRoles.TryGetValue(student.Email, out var expectedRole))
            {
                student.Roles = expectedRole;
            }
        }
        context.SaveChanges();

        // Enroll Alice in the seeded SCORM course ("Introduction to C#" - course ID from CatalogSeeder)
        var scormCourseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var aliceId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");

        var existingEnrollment = context.Enrollments
            .FirstOrDefault(e => e.StudentId == aliceId && e.CourseId == scormCourseId);

        if (existingEnrollment is null)
        {
            context.Enrollments.Add(new LibreLms.Modules.Enrollment.Domain.Enrollment
            {
                StudentId = aliceId,
                CourseId = scormCourseId,
                EnrolledAt = DateTimeOffset.UtcNow
            });
            context.SaveChanges();
        }
    }
}
