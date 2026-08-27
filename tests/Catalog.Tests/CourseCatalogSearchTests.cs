using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests;

/// <summary>
/// Integration tests for the BrowseAsync search, filter, and pagination functionality.
/// Requires a running MSSQL instance (sibling 'mssql' container) and the same
/// database the Host uses. The schema and the BrowseCourses stored procedure are
/// owned by the Host's EF migrations — Database.Migrate() below applies them when
/// the app has not already done so (no-op otherwise).
/// </summary>
public class CourseCatalogSearchTests : IAsyncLifetime
{
    private CatalogDbContext? _context;
    private CourseCatalogService? _service;

    public Task InitializeAsync()
    {
        // Build connection string from environment or use default
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("Sql")
            ?? "Server=localhost,1433;Database=LearningLms;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

        // The EF migrations for all module contexts live in the Host assembly, so the
        // test context must point MigrationsAssembly at it. Database.Migrate() applies
        // pending Catalog migrations (which own the BrowseCourses stored procedure);
        // it is a no-op when the app has already migrated the shared database.
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;

        _context = new CatalogDbContext(options);
        _context.Database.Migrate();

        _service = new CourseCatalogService(_context);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Organization for all seeded rows. Courses has a unique index on
    /// (Title, OrganizationId), so cleanups are scoped to this org.
    /// </summary>
    private static readonly Guid TestOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private async Task SeedTestCoursesAsync(IEnumerable<string> titles, string category = "TestCategory")
    {
        if (_context == null) throw new InvalidOperationException("Not initialized");

        var titleList = titles.ToList();

        // Clear conflicting rows first: Courses has a unique index on
        // (Title, OrganizationId), and another test in this class (or a prior run)
        // may already use some of these titles under a different category.
        var toRemove = _context.Courses
            .Where(c => c.Category == category
                        || (c.OrganizationId == TestOrgId && titleList.Contains(c.Title)))
            .ToList();
        _context.Courses.RemoveRange(toRemove);
        await _context.SaveChangesAsync();

        // Insert test courses
        foreach (var title in titleList)
        {
            _context.Courses.Add(new LibreLms.Modules.Catalog.Domain.Course
            {
                Id = Guid.NewGuid(),
                Title = title,
                ShortDescription = $"Description for {title}",
                FullDescription = $"Full description for {title}",
                Category = category,
                Duration = "1 hour",
                OrganizationId = TestOrgId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }

    // ===== Integration Tests (Phase 7) =====

    [Fact]
    public async Task BrowseAsync_returns_matching_courses_by_title()
    {
        await SeedTestCoursesAsync(new[]
        {
            "Python Programming", "Java Basics", "JavaScript Guide",
            "C# Advanced", "Data Science Intro", "Data Engineering 101",
            "Machine Learning"
        }, "TestIntegration");

        var result = await _service!.BrowseAsync("data", "TestIntegration", 1, 12);

        Assert.NotEmpty(result.Items);
        foreach (var item in result.Items)
        {
            Assert.Contains("data", item.Title.ToLowerInvariant());
        }
    }

    [Fact]
    public async Task BrowseAsync_filters_by_category()
    {
        await SeedTestCoursesAsync(new[]
        {
            "Course A", "Course B", "Course C"
        }, "TestCategory");

        // Add some in different category (removing a leftover from a prior run
        // first — (Title, OrganizationId) is unique)
        var ctx = _context!;
        var staleOther = await ctx.Courses
            .Where(c => c.Title == "Other Course" && c.OrganizationId == TestOrgId)
            .ToListAsync();
        ctx.Courses.RemoveRange(staleOther);
        ctx.Courses.Add(new LibreLms.Modules.Catalog.Domain.Course
        {
            Id = Guid.NewGuid(), Title = "Other Course",
            ShortDescription = "Other", FullDescription = "Other",
            Category = "OtherCategory", Duration = "1h",
            OrganizationId = TestOrgId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await _service!.BrowseAsync(null, "TestCategory", 1, 12);

        Assert.All(result.Items, item => Assert.Equal("TestCategory", item.Category));
    }

    [Fact]
    public async Task BrowseAsync_combines_search_and_category()
    {
        await SeedTestCoursesAsync(new[]
        {
            "Python Programming", "Python Advanced"
        }, "CombinedTest");

        // Add courses in other category with "Python" in title (removing a
        // leftover from a prior run first — (Title, OrganizationId) is unique)
        var ctx = _context!;
        var staleCross = await ctx.Courses
            .Where(c => c.Title == "Python for Data Science" && c.OrganizationId == TestOrgId)
            .ToListAsync();
        ctx.Courses.RemoveRange(staleCross);
        ctx.Courses.Add(new LibreLms.Modules.Catalog.Domain.Course
        {
            Id = Guid.NewGuid(), Title = "Python for Data Science",
            ShortDescription = "Cross-category", FullDescription = "Cross",
            Category = "OtherCategory", Duration = "1h",
            OrganizationId = TestOrgId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await _service!.BrowseAsync("python", "CombinedTest", 1, 12);

        Assert.All(result.Items, item =>
        {
            Assert.Contains("python", item.Title.ToLowerInvariant());
            Assert.Equal("CombinedTest", item.Category);
        });
    }

    [Fact]
    public async Task BrowseAsync_returns_correct_page()
    {
        var titles = Enumerable.Range(1, 30).Select(i => $"Test Course {i:D3}").ToList();
        await SeedTestCoursesAsync(titles, "PaginationTest");

        var page1 = await _service!.BrowseAsync(null, "PaginationTest", 1, 10);
        var page2 = await _service!.BrowseAsync(null, "PaginationTest", 2, 10);

        Assert.Equal(10, page1.Items.Count());
        Assert.Equal(10, page2.Items.Count());

        // Pages should have different, non-overlapping results
        var page1Ids = page1.Items.Select(c => c.Id).ToHashSet();
        var page2Ids = page2.Items.Select(c => c.Id).ToHashSet();
        Assert.Empty(page1Ids.Intersect(page2Ids));
    }

    [Fact]
    public async Task BrowseAsync_total_count_across_pages_matches()
    {
        var titles = Enumerable.Range(1, 25).Select(i => $"Count Course {i:D3}").ToList();
        await SeedTestCoursesAsync(titles, "CountTest");

        var pageSize = 10;
        var allItems = new List<LibreLms.Modules.Catalog.Application.CourseItemDto>();
        var page = 1;

        while (true)
        {
            var result = await _service!.BrowseAsync(null, "CountTest", page, pageSize);
            allItems.AddRange(result.Items);
            if (result.Items.Count() < pageSize) break;
            page++;
        }

        Assert.Equal(25, allItems.Count);
    }

    [Fact]
    public async Task BrowseAsync_empty_result_for_no_match()
    {
        var result = await _service!.BrowseAsync("xyznonexistent123", null, 1, 12);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ===== Performance Tests (Phase 6) =====

    [Fact]
    public async Task Stored_procedure_returns_page_within_200ms_with_100_courses()
    {
        var titles = Enumerable.Range(1, 100).Select(i => $"Perf Course {i:D4}").ToList();
        await SeedTestCoursesAsync(titles, "PerfTest100");

        var stopwatch = Stopwatch.StartNew();
        var result = await _service!.BrowseAsync("perf", "PerfTest100", 1, 12);
        stopwatch.Stop();

        Assert.True(result.Items.Count() <= 12, $"Expected ≤12 items, got {result.Items.Count()}");
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"SP took {stopwatch.ElapsedMilliseconds}ms, expected < 200ms");
    }

    [Fact]
    public async Task Stored_procedure_returns_page_within_500ms_with_1000_courses()
    {
        var titles = Enumerable.Range(1, 1000).Select(i => $"Perf Course {i:D5}").ToList();
        await SeedTestCoursesAsync(titles, "PerfTest1000");

        var stopwatch = Stopwatch.StartNew();
        var result = await _service!.BrowseAsync("perf", "PerfTest1000", 1, 12);
        stopwatch.Stop();

        Assert.True(result.Items.Count() <= 12, $"Expected ≤12 items, got {result.Items.Count()}");
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"SP took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
    }

    [Fact]
    public async Task Stored_procedure_returns_page_within_1000ms_with_10000_courses()
    {
        var titles = Enumerable.Range(1, 10000).Select(i => $"Perf Course {i:D6}").ToList();
        await SeedTestCoursesAsync(titles, "PerfTest10K");

        var stopwatch = Stopwatch.StartNew();
        var result = await _service!.BrowseAsync("perf", "PerfTest10K", 5, 12);
        stopwatch.Stop();

        Assert.Equal(12, result.Items.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"SP took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public async Task BrowseAsync_service_method_within_500ms()
    {
        var titles = Enumerable.Range(1, 500).Select(i => $"Service Test Course {i:D4}").ToList();
        await SeedTestCoursesAsync(titles, "ServicePerf");

        var stopwatch = Stopwatch.StartNew();
        var result = await _service!.BrowseAsync("service", "ServicePerf", 1, 12);
        stopwatch.Stop();

        Assert.True(result.Items.Count() <= 12);
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Full-stack call took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
    }

}
