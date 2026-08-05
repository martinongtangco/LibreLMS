using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests;

/// <summary>
/// Integration tests for the BrowseAsync search, filter, and pagination functionality.
/// Tests require a running MSSQL instance (docker compose up mssql).
/// </summary>
public class CourseCatalogSearchTests : IAsyncLifetime
{
    private string? _connectionString;
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

        _connectionString = config.GetConnectionString("Sql")
            ?? "Server=localhost,1433;Database=LearningLms;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        _context = new CatalogDbContext(options);
        _context.Database.OpenConnection();

        // Ensure the stored procedure exists
        EnsureStoredProceduresExist();

        _service = new CourseCatalogService(_context);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    private void EnsureStoredProceduresExist()
    {
        using var cmd = new SqlCommand(@"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = 'BrowseCourses' AND type = 'P')
            BEGIN
                -- Create Full-Text Catalog
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'LearningLmsFtCatalog')
                    CREATE FULLTEXT CATALOG LearningLmsFtCatalog AS DEFAULT;

                -- Create Full-Text Index
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes fti
                               JOIN sys.tables t ON fti.object_id = t.object_id
                               WHERE t.name = 'Courses')
                    CREATE FULLTEXT INDEX ON Courses(Title)
                    KEY INDEX UK_Title_OrganizationId
                    CATALOG LearningLmsFtCatalog
                    WITH (CHANGE_TRACKING AUTO);

                -- Create stored procedure
                CREATE PROCEDURE BrowseCourses
                    @SearchTerm NVARCHAR(200) = NULL,
                    @Category NVARCHAR(100) = NULL,
                    @OrganizationIdScope UNIQUEIDENTIFIER = NULL,
                    @VisibleCourseIds UNIQUEIDENTIFIER[] = NULL,
                    @PageSize INT = 12,
                    @PageNumber INT = 1
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF @PageSize <= 0 SET @PageSize = 12;
                    IF @PageNumber <= 0 SET @PageNumber = 1;
                    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
                    DECLARE @FtsAvailable BIT = 0;
                    SELECT @FtsAvailable = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
                    FROM sys.fulltext_indexes fti
                    JOIN sys.tables t ON fti.object_id = t.object_id
                    WHERE t.name = 'Courses';

                    SELECT c.Id, c.Title, c.ShortDescription, c.Category, c.Duration
                    FROM Courses c
                    WHERE (@VisibleCourseIds IS NULL OR EXISTS (SELECT 1 FROM @VisibleCourseIds v WHERE v.value = c.Id))
                        AND (@OrganizationIdScope IS NULL OR c.OrganizationId = @OrganizationIdScope)
                        AND (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        AND (@SearchTerm IS NULL OR @SearchTerm = ''
                            OR ((@FtsAvailable = 1 AND CONTAINS(c.Title, @SearchTerm))
                                OR (@FtsAvailable = 0 AND c.Title LIKE '%' + @SearchTerm + '%')))
                    ORDER BY c.Title ASC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                    SELECT COUNT(*) AS TotalCount FROM Courses c
                    WHERE (@VisibleCourseIds IS NULL OR EXISTS (SELECT 1 FROM @VisibleCourseIds v WHERE v.value = c.Id))
                        AND (@OrganizationIdScope IS NULL OR c.OrganizationId = @OrganizationIdScope)
                        AND (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        AND (@SearchTerm IS NULL OR @SearchTerm = ''
                            OR ((@FtsAvailable = 1 AND CONTAINS(c.Title, @SearchTerm))
                                OR (@FtsAvailable = 0 AND c.Title LIKE '%' + @SearchTerm + '%')));
                END;
            END;
        ", new SqlConnection(_connectionString!));
        cmd.ExecuteNonQuery();
    }

    private async Task SeedTestCoursesAsync(IEnumerable<string> titles, string category = "TestCategory")
    {
        if (_context == null) throw new InvalidOperationException("Not initialized");

        // Clear test data first
        var testCourses = _context.Courses.Where(c => c.Category == category).ToList();
        _context.Courses.RemoveRange(testCourses);
        await _context.SaveChangesAsync();

        // Insert test courses
        foreach (var title in titles)
        {
            _context.Courses.Add(new LibreLms.Modules.Catalog.Domain.Course
            {
                Id = Guid.NewGuid(),
                Title = title,
                ShortDescription = $"Description for {title}",
                FullDescription = $"Full description for {title}",
                Category = category,
                Duration = "1 hour",
                OrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }

    // ===== Integration Tests (Phase 7) =====

    [Fact(Skip = "Requires MSSQL")]
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

    [Fact(Skip = "Requires MSSQL")]
    public async Task BrowseAsync_filters_by_category()
    {
        await SeedTestCoursesAsync(new[]
        {
            "Course A", "Course B", "Course C"
        }, "TestCategory");

        // Add some in different category
        var ctx = _context!;
        ctx.Courses.Add(new LibreLms.Modules.Catalog.Domain.Course
        {
            Id = Guid.NewGuid(), Title = "Other Course",
            ShortDescription = "Other", FullDescription = "Other",
            Category = "OtherCategory", Duration = "1h",
            OrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await _service!.BrowseAsync(null, "TestCategory", 1, 12);

        Assert.All(result.Items, item => Assert.Equal("TestCategory", item.Category));
    }

    [Fact(Skip = "Requires MSSQL")]
    public async Task BrowseAsync_combines_search_and_category()
    {
        await SeedTestCoursesAsync(new[]
        {
            "Python Programming", "Python Advanced"
        }, "CombinedTest");

        // Add courses in other category with "Python" in title
        var ctx = _context!;
        ctx.Courses.Add(new LibreLms.Modules.Catalog.Domain.Course
        {
            Id = Guid.NewGuid(), Title = "Python for Data Science",
            ShortDescription = "Cross-category", FullDescription = "Cross",
            Category = "OtherCategory", Duration = "1h",
            OrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
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

    [Fact(Skip = "Requires MSSQL")]
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

    [Fact(Skip = "Requires MSSQL")]
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

    [Fact(Skip = "Requires MSSQL")]
    public async Task BrowseAsync_empty_result_for_no_match()
    {
        var result = await _service!.BrowseAsync("xyznonexistent123", null, 1, 12);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ===== Performance Tests (Phase 6) =====

    [Fact(Skip = "Requires MSSQL")]
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

    [Fact(Skip = "Requires MSSQL")]
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

    [Fact(Skip = "Requires MSSQL")]
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

    [Fact(Skip = "Requires MSSQL")]
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

    [Fact(Skip = "Requires MSSQL")]
    public async Task FTS_index_exists_after_migration()
    {
        using var cmd = new SqlCommand(@"
            SELECT COUNT(*) FROM sys.fulltext_indexes fti
            JOIN sys.tables t ON fti.object_id = t.object_id
            WHERE t.name = 'Courses'
        ", new SqlConnection(_connectionString!));

        var count = (int)cmd.ExecuteScalar();
        Assert.True(count >= 1, "FTS index should exist on Courses table");
    }

    [Fact(Skip = "Requires MSSQL")]
    public async Task Stored_procedure_fallback_to_like_when_fts_unavailable()
    {
        // Seed some courses
        await SeedTestCoursesAsync(new[] { "Fallback Test Alpha", "Fallback Test Beta", "Regular Course" }, "FallbackTest");

        // Temporarily disable FTS by dropping the index
        using (var dropCmd = new SqlCommand(@"
            IF EXISTS (SELECT 1 FROM sys.fulltext_indexes fti
                       JOIN sys.tables t ON fti.object_id = t.object_id
                       WHERE t.name = 'Courses')
                DROP FULLTEXT INDEX ON Courses;
        ", new SqlConnection(_connectionString!)))
        {
            dropCmd.ExecuteNonQuery();
        }

        // Call stored procedure — should fall back to LIKE
        var result = await _service!.BrowseAsync("fallback", "FallbackTest", 1, 12);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Contains("fallback", item.Title.ToLowerInvariant()));

        // Recreate FTS index for other tests
        using (var recreateCmd = new SqlCommand(@"
            IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes fti
                           JOIN sys.tables t ON fti.object_id = t.object_id
                           WHERE t.name = 'Courses')
                CREATE FULLTEXT INDEX ON Courses(Title)
                KEY INDEX UK_Title_OrganizationId
                CATALOG LearningLmsFtCatalog
                WITH (CHANGE_TRACKING AUTO);
        ", new SqlConnection(_connectionString!)))
        {
            recreateCmd.ExecuteNonQuery();
        }
    }
}
