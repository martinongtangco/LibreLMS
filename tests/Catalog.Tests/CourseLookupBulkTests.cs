using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Domain;
using LibreLms.Modules.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests;

/// <summary>
/// Spec 048 (E7) — integration tests for the two bulk <c>ICourseLookup</c>
/// implementations added to <c>CourseLookup</c>, following this project's
/// existing real-SQL pattern (CourseCatalogSearchTests): connection string from
/// the environment, Host-assembly migrations, test-organization scoping with
/// cleanup.
/// </summary>
public class CourseLookupBulkTests : IAsyncLifetime
{
    private CatalogDbContext? _context;
    private CourseLookup? _lookup;

    // Fixed test orgs — courses have a unique index on (Title, OrganizationId),
    // so cleanup by org id is safe and idempotent.
    private static readonly Guid OrgOne = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid OrgTwo = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid OrgZero = Guid.Parse("00000000-0000-0000-0000-0000000000b3");

    public Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? "Server=localhost,1433;Database=LearningLms;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;

        _context = new CatalogDbContext(options);
        _context.Database.Migrate();
        _lookup = new CourseLookup(_context);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_context is null)
            return;

        // Idempotent cleanup of this test's seeded rows.
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM Courses WHERE OrganizationId IN ({0}, {1}, {2})", OrgOne, OrgTwo, OrgZero);
        _context.Dispose();
    }

    [Fact]
    public async Task GetCourseCountsByOrgsAsync_ReturnsPerOrgCounts_InOneResult()
    {
        SeedCourse(OrgOne, "048-Bulk-A1", "Ref");
        SeedCourse(OrgOne, "048-Bulk-A2", "Ref");
        SeedCourse(OrgTwo, "048-Bulk-B1", "Ref");
        // OrgZero deliberately has no courses.

        var counts = await _lookup!.GetCourseCountsByOrgsAsync(new[] { OrgOne, OrgTwo, OrgZero });

        Assert.Equal(2, counts[OrgOne]);
        Assert.Equal(1, counts[OrgTwo]);
        Assert.False(counts.ContainsKey(OrgZero), "zero-course orgs are absent from the dict");
    }

    [Fact]
    public async Task GetCourseCountsByOrgsAsync_EmptyInput_ReturnsEmptyDictionary()
    {
        var counts = await _lookup!.GetCourseCountsByOrgsAsync(Array.Empty<Guid>());

        Assert.Empty(counts);
    }

    [Fact]
    public async Task GetDistinctCategoriesAsync_MatchesDirectQuery_OrderedAndDistinct()
    {
        // The method is catalog-wide; compare against a direct EF query so the
        // assertion is robust to whatever else the shared dev database contains.
        SeedCourse(OrgOne, "048-Cat-X1", "048-Zebra");
        SeedCourse(OrgOne, "048-Cat-X2", "048-Zebra"); // duplicate category
        SeedCourse(OrgTwo, "048-Cat-Y1", "048-Alpha");

        var actual = await _lookup!.GetDistinctCategoriesAsync();

        var expected = await _context!.Courses
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        Assert.Equal(expected, actual);
        // Sanity for the seeded rows themselves.
        Assert.Contains("048-Alpha", actual);
        Assert.Contains("048-Zebra", actual);
        Assert.Single(actual, c => c == "048-Zebra");
    }

    // ── Helpers ──

    private void SeedCourse(Guid orgId, string title, string category)
    {
        _context!.Courses.Add(new Course
        {
            Title = title,
            ShortDescription = "bulk test course",
            FullDescription = "bulk test course",
            Category = category,
            Duration = "1h",
            OrganizationId = orgId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _context.SaveChanges();
    }
}
