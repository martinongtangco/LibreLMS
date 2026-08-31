using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Scorm.Tests;

/// <summary>
/// Spec 048 (E6) — unit test for the new bulk contract method
/// <c>ScormPackageService.GetCourseIdsWithPackagesAsync</c>: the admin course list
/// previously called <c>GetPackageByCourseIdAsync</c> once per row (up to 100/page);
/// the bulk method must resolve the whole page with a single
/// <c>WHERE CourseId IN @ids</c> query and return exactly the ids that have a
/// package.
///
/// Real MSSQL via ConnectionStrings__Sql (migrations live in the Host assembly),
/// same pattern as the other Scorm.Tests. ScormPackages has no FK to Courses, so
/// package rows can reference random course GUIDs (the admin page joins in memory).
/// </summary>
public class GetCourseIdsWithPackagesBulkTests : IAsyncLifetime
{
    private readonly Guid _courseWithPackageA = Guid.NewGuid();
    private readonly Guid _courseWithPackageB = Guid.NewGuid();
    private readonly Guid _courseWithoutPackage = Guid.NewGuid();
    private string _sqlConn = null!;

    public async Task InitializeAsync()
    {
        _sqlConn = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? throw new InvalidOperationException("ConnectionStrings__Sql environment variable is required.");

        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
    }

    // Each test cleans up its own seed rows (random course GUIDs per run).
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReturnsExactlyTheCourseIdsThatHavePackages()
    {
        await using var ctx = NewContext();

        var packageA = new ScormPackage
        {
            CourseId = _courseWithPackageA,
            ManifestTitle = "Package A",
            LaunchPath = "index.html",
            ContentDirectory = $"scorm-content/{Guid.NewGuid():N}"
        };
        var packageB = new ScormPackage
        {
            CourseId = _courseWithPackageB,
            ManifestTitle = "Package B",
            LaunchPath = "index.html",
            ContentDirectory = $"scorm-content/{Guid.NewGuid():N}"
        };
        // An unassociated (available-pool) package: CourseId = null must never match.
        var poolPackage = new ScormPackage
        {
            CourseId = null,
            ManifestTitle = "Pool Package",
            LaunchPath = "index.html",
            ContentDirectory = $"scorm-content/{Guid.NewGuid():N}"
        };
        ctx.ScormPackages.AddRange(packageA, packageB, poolPackage);
        await ctx.SaveChangesAsync();

        try
        {
            var service = new ScormPackageService(ctx, new ManifestParser(), Path.GetTempPath());

            var result = (await service.GetCourseIdsWithPackagesAsync(
                new[] { _courseWithPackageA, _courseWithPackageB, _courseWithoutPackage })).ToList();

            Assert.Equal(new[] { _courseWithPackageA, _courseWithPackageB }.ToHashSet(), result.ToHashSet());
            Assert.DoesNotContain(_courseWithoutPackage, result);
        }
        finally
        {
            // Cleanup the seed rows (entities are still tracked in this context).
            ctx.ScormPackages.RemoveRange(new[] { packageA, packageB, poolPackage });
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task EmptyInput_ReturnsEmptyWithoutHittingTheDatabase()
    {
        // Point the context at an unreachable server: if the method issued ANY
        // query, the connection attempt would throw. Empty input must short-circuit.
        var unreachable = NewContext("Server=127.0.0.1,59999;Database=Nope;User Id=sa;Password=none;TrustServerCertificate=True;Connect Timeout=2");
        var service = new ScormPackageService(unreachable, new ManifestParser(), Path.GetTempPath());

        var result = await service.GetCourseIdsWithPackagesAsync(Array.Empty<Guid>());

        Assert.Empty(result);
    }

    // ── Helpers ──

    private ScormDbContext NewContext(string? connStr = null)
    {
        connStr ??= _sqlConn;
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<ScormDbContext>()
            .UseSqlServer(connStr, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;
        return new ScormDbContext(options);
    }
}
