using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Infrastructure;

/// <summary>
/// Seeds the root organization on first startup.
/// Spec 027: the seeded SuperUser Student row moved to EnrollmentSeeder so the
/// Management module no longer reaches into Enrollment internals (boundary gate, R9).
/// </summary>
public static class ManagementSeeder
{
    public static void Seed(ManagementDbContext managementCtx)
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
    }
}
