using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Infrastructure;

namespace Catalog.Tests;

/// <summary>
/// Integration tests for the BrowseCourses visible-course filter (spec 054,
/// ADR 0012): the visible-id set moves INTO the procedure as
/// @VisibleCourseIds NVARCHAR(MAX) (JSON array of GUIDs; NULL = legacy
/// unfiltered), so filtering, paging and counting agree.
///
/// TDD note: the 7th parameter does not exist until the spec 054 migration
/// lands — the 7-parameter SP calls fail at runtime against the current
/// procedure ("too many arguments"), and the service-level filtered-count
/// assertions fail because BrowseAsync still pages/counts unfiltered and
/// strips rows in memory (TotalCount returns the unfiltered 13).
///
/// House marker pattern (cf. BrowseCoursesSortTests): marker-prefixed
/// courses inserted directly, every assertion scoped to the marker, cleanup
/// in Initialize/Dispose. Requires the shared MSSQL instance + the Host
/// migration assembly (applied via Database.Migrate).
/// </summary>
public class BrowseCoursesVisibilityTests : IAsyncLifetime
{
    private sealed record Row(Guid Id, string Title);

    private const string Marker = "AdmPg054C";
    private static readonly Guid Org = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private string _connectionString = null!;
    private CatalogDbContext _context = null!;
    private CourseCatalogService _service = null!;
    private List<Guid> _all = new();     // insertion order
    private List<Guid> _visible = new(); // the 8 visible ids

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? "Server=mssql,1433;Database=LearningLms;User Id=sa;Password=Lms#vZdV361xAfdYmoEZmTmh!9;TrustServerCertificate=True";

        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;

        _context = new CatalogDbContext(options);
        _context.Database.Migrate();

        await DeleteFillerAsync();

        for (var i = 1; i <= 13; i++)
        {
            var id = Guid.NewGuid();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Courses (Id, Title, ShortDescription, FullDescription, Category, Duration, OrganizationId, CreatedAt)
                VALUES (@Id, @Title, @ShortDescription, @FullDescription, @Category, @Duration, @OrganizationId, SYSDATETIMEOFFSET());", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Title", $"{Marker} T{i:D2}");
            cmd.Parameters.AddWithValue("@ShortDescription", "filler");
            cmd.Parameters.AddWithValue("@FullDescription", "filler");
            cmd.Parameters.AddWithValue("@Category", $"{Marker} Cat");
            cmd.Parameters.AddWithValue("@Duration", "1 hour");
            cmd.Parameters.AddWithValue("@OrganizationId", Org);
            await cmd.ExecuteNonQueryAsync();

            _all.Add(id);
        }

        // Visible set: first 8 in insertion order; the last 5 are "hidden".
        _visible = _all.Take(8).ToList();
        _service = new CourseCatalogService(_context);
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

    private static string ToJson(params Guid[] ids)
        => "[" + string.Join(",", ids.Select(i => $"\"{i}\"")) + "]";

    /// <summary>
    /// 7-parameter BrowseCourses call (spec 054 shape).
    /// </summary>
    private async Task<(IList<Row> Rows, int Total)> CallSpAsync(
        int pageSize, int pageNumber, string? visibleJson)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("BrowseCourses", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add("@SearchTerm", SqlDbType.NVarChar, 200).Value = Marker;
        cmd.Parameters.Add("@Category", SqlDbType.NVarChar, 100).Value = (object)DBNull.Value;
        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
        cmd.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;
        cmd.Parameters.Add("@SortBy", SqlDbType.NVarChar, 20).Value = "title";
        cmd.Parameters.Add("@SortDirection", SqlDbType.NVarChar, 4).Value = "asc";
        cmd.Parameters.Add("@VisibleCourseIds", SqlDbType.NVarChar, -1).Value = (object?)visibleJson ?? DBNull.Value;

        var rows = new List<Row>();
        var total = 0;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
            rows.Add(new Row(reader.GetGuid(0), reader.GetString(1)));

        await reader.NextResultAsync();
        if (reader.Read())
            total = reader.GetInt32(0);

        return (rows, total);
    }

    // ---- SP level: the 7th parameter filters rows AND the count ----

    [Fact]
    public async Task sp_filters_rows_and_count_to_the_visible_set()
    {
        var (rows, total) = await CallSpAsync(pageSize: 13, pageNumber: 1, visibleJson: ToJson([.. _visible]));

        Assert.Equal(8, total);                       // RED pre-fix: SP counts all 13 (param doesn't exist yet)
        Assert.Equal(8, rows.Count);
        Assert.True(rows.All(r => _visible.Contains(r.Id)));
    }

    [Fact]
    public async Task sp_pages_full_pages_then_remainder_and_never_overshoots()
    {
        // pageSize 5 over 8 visible: page 1 full (5), page 2 = 3 (the
        // remainder), pages 3+ empty. Every page's rows are visible.
        var (p1, t1) = await CallSpAsync(5, 1, ToJson([.. _visible]));
        var (p2, t2) = await CallSpAsync(5, 2, ToJson([.. _visible]));
        var (p3, t3) = await CallSpAsync(5, 3, ToJson([.. _visible]));

        Assert.Equal(8, t1); Assert.Equal(8, t2); Assert.Equal(8, t3);
        Assert.Equal(5, p1.Count);
        Assert.Equal(3, p2.Count);
        Assert.Empty(p3);
        Assert.True(p1.All(r => _visible.Contains(r.Id)));
        Assert.True(p2.All(r => _visible.Contains(r.Id)));
        Assert.DoesNotContain(p1, r => p2.Any(b => b.Id == r.Id)); // disjoint pages
    }

    [Fact]
    public async Task sp_null_param_keeps_legacy_unfiltered_behavior()
    {
        var (rows, total) = await CallSpAsync(pageSize: 13, pageNumber: 1, visibleJson: null);

        Assert.Equal(13, total);
        Assert.Equal(13, rows.Count);
    }

    [Fact]
    public async Task sp_empty_json_array_hides_everything()
    {
        var (rows, total) = await CallSpAsync(pageSize: 13, pageNumber: 1, visibleJson: "[]");

        Assert.Equal(0, total);
        Assert.Empty(rows);
    }

    // ---- Service level: BrowseAsync passes the set through (no in-memory filter) ----

    [Fact]
    public async Task browse_async_reports_the_visible_total_not_the_unfiltered_total()
    {
        var result = await _service.BrowseAsync(
            Marker, null, pageNumber: 1, pageSize: 13, visibleCourseIds: new HashSet<Guid>(_visible));

        Assert.Equal(8, result.TotalCount);   // RED pre-fix: 13 (count is the SP's unfiltered total)
        Assert.Equal(8, result.Items.Count());
        Assert.DoesNotContain(result.Items, i => !_visible.Contains(i.Id));
    }

    [Fact]
    public async Task browse_async_empty_visible_set_sees_nothing()
    {
        // The old in-memory filter only applied for non-empty sets — an org
        // with zero visible courses saw everything. Must be 0/0.
        var result = await _service.BrowseAsync(
            Marker, null, pageNumber: 1, pageSize: 13, visibleCourseIds: new HashSet<Guid>());

        Assert.Equal(0, result.TotalCount);   // RED pre-fix: 13
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task browse_async_null_set_is_unfiltered()
    {
        var result = await _service.BrowseAsync(
            Marker, null, pageNumber: 1, pageSize: 13, visibleCourseIds: null);

        Assert.Equal(13, result.TotalCount);
        Assert.Equal(13, result.Items.Count());
    }
}
