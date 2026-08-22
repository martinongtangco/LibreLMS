using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Catalog.Infrastructure;

namespace Catalog.Tests;

/// <summary>
/// Integration tests for the EXTENDED BrowseCourses stored procedure (spec 032, US4):
/// server-side sorting by title/category/duration in asc/desc, the added OrganizationId
/// result column, input normalization/fallbacks, and the legacy four-parameter call that
/// must reproduce the old title-ascending behavior (FR-017).
///
/// TDD note: the extended procedure is created by an EF Core migration on the Catalog
/// context (applied below via Database.Migrate). Until that migration's SQL exists, the
/// six-parameter calls fail at runtime — the expected red state.
///
/// Requires a running MSSQL instance (sibling 'mssql' container) and the same database
/// the Host uses. Tests create marker-prefixed filler courses and clean them up, so they
/// pass regardless of prior data state (seeded catalog and other rows are irrelevant —
/// every assertion is scoped to the "AdmPg032C" marker).
/// </summary>
public class BrowseCoursesSortTests : IAsyncLifetime
{
    /// <summary>One course row as returned by BrowseCourses result set 1 (columns 0..5).</summary>
    private sealed record BrowseRowTest(
        Guid Id, string Title, string ShortDescription,
        string Category, string Duration, Guid OrganizationId);

    /// <summary>A filler course recorded at seed time (insertion order).</summary>
    private sealed record FillerCourse(Guid Id, string Title, bool EvenIndex);

    private const string Marker = "AdmPg032C";
    private static readonly Guid EvenOrg = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OddOrg = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private string _connectionString = null!;
    private CatalogDbContext _context = null!;
    private List<FillerCourse> _filler = new();

    public async Task InitializeAsync()
    {
        // Connection string from the environment (devcontainer sets ConnectionStrings__Sql),
        // falling back to the docker-compose defaults.
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? "Server=mssql,1433;Database=LearningLms;User Id=sa;Password=Lms#vZdV361xAfdYmoEZmTmh!9;TrustServerCertificate=True";

        // The EF migrations for all module contexts live in the Host assembly, so the
        // test context must point MigrationsAssembly at it. Database.Migrate() applies
        // pending Catalog migrations (including the one that will recreate BrowseCourses
        // with the sort parameters); it is a no-op until that migration's SQL exists.
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;

        _context = new CatalogDbContext(options);
        _context.Database.Migrate();  // Migrate() is synchronous (returns void)

        await DeleteFillerAsync();

        // Seed 13 filler courses crafted so in-page order != full-set order under each
        // sort key: titles are inserted in descending title order (T13 first, T01 last),
        // categories alternate by index, durations cycle through four distinct values.
        for (var i = 1; i <= 13; i++)
        {
            var id = Guid.NewGuid();
            var title = $"{Marker} T{(14 - i):D2}";
            var category = i % 2 == 0 ? $"{Marker} Even" : $"{Marker} Odd";
            var duration = (i % 4) switch
            {
                1 => "1 hour",
                2 => "2 hours",
                3 => "3 hours",
                _ => "4 hours"
            };
            var orgId = i % 2 == 0 ? EvenOrg : OddOrg;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Courses (Id, Title, ShortDescription, FullDescription, Category, Duration, OrganizationId, CreatedAt)
                VALUES (@Id, @Title, @ShortDescription, @FullDescription, @Category, @Duration, @OrganizationId, SYSDATETIMEOFFSET());", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Title", title);
            cmd.Parameters.AddWithValue("@ShortDescription", "filler");
            cmd.Parameters.AddWithValue("@FullDescription", "filler");
            cmd.Parameters.AddWithValue("@Category", category);
            cmd.Parameters.AddWithValue("@Duration", duration);
            cmd.Parameters.AddWithValue("@OrganizationId", orgId);
            await cmd.ExecuteNonQueryAsync();

            _filler.Add(new FillerCourse(id, title, i % 2 == 0));
        }
    }

    public async Task DisposeAsync()
    {
        await DeleteFillerAsync();
        _context?.Dispose();
    }

    private async Task DeleteFillerAsync()
    {
        if (_connectionString == null)
            return;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand($"DELETE FROM Courses WHERE Title LIKE '{Marker}%';", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Calls BrowseCourses with the full six-parameter shape (spec 032 extension).
    /// Returns the page rows (columns 0..5) plus the filtered total count.
    /// </summary>
    private async Task<(IList<BrowseRowTest> Rows, int TotalCount)> CallSpAsync(
        string? searchTerm, string? category, int pageSize, int pageNumber,
        string sortBy, string sortDirection)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("BrowseCourses", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add("@SearchTerm", SqlDbType.NVarChar, 200).Value = searchTerm ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Category", SqlDbType.NVarChar, 100).Value = category ?? (object)DBNull.Value;
        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
        cmd.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
        cmd.Parameters.Add("@SortBy", SqlDbType.NVarChar, 20).Value = sortBy;
        cmd.Parameters.Add("@SortDirection", SqlDbType.NVarChar, 4).Value = sortDirection;

        var rows = new List<BrowseRowTest>();
        var totalCount = 0;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (reader.Read())
        {
            rows.Add(new BrowseRowTest(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetGuid(5)));
        }

        await reader.NextResultAsync();
        if (reader.Read())
        {
            totalCount = reader.GetInt32(0);
        }

        return (rows, totalCount);
    }

    /// <summary>
    /// Legacy call shape (FR-017): the original four parameters only. The procedure's
    /// defaults must reproduce the pre-extension title-ascending behavior.
    /// </summary>
    private async Task<(IList<BrowseRowTest> Rows, int TotalCount)> CallSpLegacyAsync(
        string? searchTerm, string? category, int pageSize, int pageNumber)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("BrowseCourses", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add("@SearchTerm", SqlDbType.NVarChar, 200).Value = searchTerm ?? (object)DBNull.Value;
        cmd.Parameters.Add("@Category", SqlDbType.NVarChar, 100).Value = category ?? (object)DBNull.Value;
        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
        cmd.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;

        var rows = new List<BrowseRowTest>();
        var totalCount = 0;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (reader.Read())
        {
            rows.Add(new BrowseRowTest(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetGuid(5)));
        }

        await reader.NextResultAsync();
        if (reader.Read())
        {
            totalCount = reader.GetInt32(0);
        }

        return (rows, totalCount);
    }

    /// <summary>Fetches all filler rows (13 fit in two pages of 10) for a given sort.</summary>
    private async Task<IList<BrowseRowTest>> FetchAllAsync(string sortBy, string sortDirection)
    {
        var (page1, _) = await CallSpAsync(Marker, null, 10, 1, sortBy, sortDirection);
        var (page2, _) = await CallSpAsync(Marker, null, 10, 2, sortBy, sortDirection);
        return page1.Concat(page2).ToList();
    }

    [Fact]
    public async Task title_asc_across_page_boundary()
    {
        var (page1, total1) = await CallSpAsync(Marker, null, 10, 1, "title", "asc");
        var (page2, total2) = await CallSpAsync(Marker, null, 10, 2, "title", "asc");

        Assert.Equal(10, page1.Count);
        Assert.Equal(3, page2.Count);
        Assert.Equal(13, total1);
        Assert.Equal(13, total2);

        var all = page1.Concat(page2).Select(r => r.Title).ToList();
        for (var i = 1; i < all.Count; i++)
        {
            Assert.True(string.Compare(all[i - 1], all[i], StringComparison.Ordinal) < 0, $"Title order violated at position {i}: {all[i - 1]} !< {all[i]}");
        }

        // The boundary itself: last title of page 1 sorts before first title of page 2.
        Assert.True(string.Compare(page1[^1].Title, page2[0].Title, StringComparison.Ordinal) < 0);
    }

    [Fact]
    public async Task title_desc_across_page_boundary()
    {
        var all = await FetchAllAsync("title", "desc");
        Assert.Equal(13, all.Count);

        for (var i = 1; i < all.Count; i++)
        {
            Assert.True(string.Compare(all[i - 1].Title, all[i].Title, StringComparison.Ordinal) > 0, $"Title desc violated at position {i}");
        }
    }

    [Fact]
    public async Task category_asc_across_page_boundary()
    {
        var (page1, _) = await CallSpAsync(Marker, null, 10, 1, "category", "asc");
        var (page2, _) = await CallSpAsync(Marker, null, 10, 2, "category", "asc");

        var all = page1.Concat(page2).ToList();
        Assert.Equal(13, all.Count);

        // 6 Even rows then 7 Odd rows: every Even must precede every Odd.
        var firstOdd = all.FindIndex(r => r.Category == $"{Marker} Odd");
        Assert.True(firstOdd >= 0);
        Assert.All(all.Skip(firstOdd), r => Assert.Equal($"{Marker} Odd", r.Category));
        Assert.Equal(6, firstOdd);

        // With 6 Even + 7 Odd and a page size of 10, page 2 holds only the last 3 Odd rows.
        Assert.All(page2, r => Assert.Equal($"{Marker} Odd", r.Category));
    }

    [Fact]
    public async Task category_desc_across_page_boundary()
    {
        var all = await FetchAllAsync("category", "desc");
        Assert.Equal(13, all.Count);

        // Every Odd row must precede every Even row.
        var firstEven = all.ToList().FindIndex(r => r.Category == $"{Marker} Even");
        Assert.True(firstEven >= 0);
        Assert.All(all.Take(firstEven), r => Assert.Equal($"{Marker} Odd", r.Category));
        Assert.Equal(7, firstEven);
    }

    [Fact]
    public async Task duration_asc_grouped()
    {
        var all = await FetchAllAsync("duration", "asc");
        Assert.Equal(13, all.Count);

        // Collation order: "1 hour" < "2 hours" < "3 hours" < "4 hours" — the concatenation
        // must be non-decreasing by Duration (groups, in that order).
        for (var i = 1; i < all.Count; i++)
        {
            Assert.True(
                string.Compare(all[i - 1].Duration, all[i].Duration, StringComparison.Ordinal) <= 0,
                $"Duration asc violated at position {i}: '{all[i - 1].Duration}' !<= '{all[i].Duration}'");
        }
    }

    [Fact]
    public async Task duration_desc_grouped()
    {
        var all = await FetchAllAsync("duration", "desc");
        Assert.Equal(13, all.Count);

        for (var i = 1; i < all.Count; i++)
        {
            Assert.True(
                string.Compare(all[i - 1].Duration, all[i].Duration, StringComparison.Ordinal) >= 0,
                $"Duration desc violated at position {i}: '{all[i - 1].Duration}' !>= '{all[i].Duration}'");
        }
    }

    [Fact]
    public async Task organization_id_present_and_correct()
    {
        var (page1, _) = await CallSpAsync(Marker, null, 10, 1, "title", "asc");
        Assert.Equal(10, page1.Count);

        foreach (var row in page1)
        {
            Assert.NotEqual(Guid.Empty, row.OrganizationId);

            var filler = _filler.First(f => f.Title == row.Title);
            var expected = filler.EvenIndex ? EvenOrg : OddOrg;
            Assert.Equal(expected, row.OrganizationId);
        }
    }

    [Fact]
    public async Task legacy_four_parameter_call_reproduces_title_asc()
    {
        // FR-017: omitting the new parameters must behave exactly like the original
        // procedure — title ascending, same page math.
        var (page1, total1) = await CallSpLegacyAsync(Marker, null, 10, 1);
        var (page2, _) = await CallSpLegacyAsync(Marker, null, 10, 2);

        Assert.Equal(10, page1.Count);
        Assert.Equal(3, page2.Count);
        Assert.Equal(13, total1);

        var all = page1.Concat(page2).Select(r => r.Title).ToList();
        for (var i = 1; i < all.Count; i++)
        {
            Assert.True(string.Compare(all[i - 1], all[i], StringComparison.Ordinal) < 0, $"Legacy title-asc violated at position {i}");
        }
    }

    [Fact]
    public async Task trailing_whitespace_is_a_like_pattern_at_sp_level()
    {
        // The SP contract treats only NULL and '' as no-filter; trailing spaces are kept
        // as part of the LIKE pattern (the C# service trims search terms before calling).
        // No filler title has the marker followed by three spaces, so the pattern matches
        // nothing — if the SP trimmed the term it would match all 13 fillers instead.
        var (rows, total) = await CallSpAsync($"{Marker}   ", null, 10, 1, "title", "asc");
        Assert.Equal(0, total);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task special_character_search_is_safe()
    {
        var (rows, total) = await CallSpAsync($"{Marker} T%_x", null, 10, 1, "title", "asc");
        Assert.Equal(0, total);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task unknown_sort_values_fall_back_to_defaults()
    {
        var (bogus, _) = await CallSpAsync(Marker, null, 10, 1, "bogus", "up");
        var (defaultCall, _) = await CallSpAsync(Marker, null, 10, 1, "title", "asc");

        Assert.Equal(
            defaultCall.Select(r => r.Id).ToList(),
            bogus.Select(r => r.Id).ToList());
    }

    [Fact]
    public async Task deterministic_across_calls()
    {
        var (first, _) = await CallSpAsync(Marker, null, 10, 1, "title", "asc");
        var (second, _) = await CallSpAsync(Marker, null, 10, 1, "title", "asc");

        Assert.Equal(first.Select(r => r.Id).ToList(), second.Select(r => r.Id).ToList());
    }
}
