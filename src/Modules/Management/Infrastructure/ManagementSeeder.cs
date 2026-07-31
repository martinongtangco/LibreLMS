using System.Security.Cryptography;
using System.Text;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.Modules.Management.Domain;
using LibreLms.SharedKernel;

namespace LibreLms.Modules.Management.Infrastructure;

/// <summary>
/// Seeds the root organization and default SuperUser account on first startup.
/// </summary>
public static class ManagementSeeder
{
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hash);
    }

    public static void Seed(ManagementDbContext managementCtx, EnrollmentDbContext enrollmentCtx)
    {
        // Create root organization
        var rootOrg = new Organization
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Root Organization",
            Description = "Top-level organization for the LMS platform",
            ParentId = null
        };
        managementCtx.Organizations.Add(rootOrg);
        managementCtx.SaveChanges();

        // Create default SuperUser assigned to root org
        var superUser = new Student
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000100"),
            Name = "System Administrator",
            Email = "admin@librelms.local",
            PasswordHash = HashPassword("Admin@12345"),
            Roles = RoleNames.SuperUser,
            OrganizationId = rootOrg.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        enrollmentCtx.Students.Add(superUser);
        enrollmentCtx.SaveChanges();
    }
}
