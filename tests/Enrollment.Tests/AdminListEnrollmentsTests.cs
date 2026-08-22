using System.Data;
using LibreLms.Modules.Enrollment.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Enrollment.Tests;

/// <summary>
/// Integration tests for the new AdminListEnrollments stored procedure
/// (spec 032, stored-procedures.md section 2).
/// Requires a running MSSQL instance (docker compose up mssql). The procedure does
/// not exist in the database yet, so these tests are expected to FAIL at runtime —
/// that is the intended TDD red state. All assertions are scoped to the "AdmPg032E"
/// filler prefix; other rows in the database (seeded users, etc.) are irrelevant.
/// </summary>
public class AdminListEnrollmentsTests : IAsyncLifetime
{
    // Root organization id shared by all module seed data.
    private static readonly Guid RootOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private string _connectionString = string.Empty;
    private EnrollmentDbContext _context = null!;

    // Filler ids created by InitializeAsync, stored in seed order.
    private readonly Guid[] _studentIds = new Guid[12];
    private readonly Guid[] _courseIds = new Guid[5];
    private Guid _orphanCourseId;

    public async Task InitializeAsync()
    {
        // Step 1: connection string from the environment, with a hard fallback.
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? "Server=mssql,1433;Database=LearningLms;User Id=sa;Password=Lms#vZdV361xAfdYmoEZmTmh!9;TrustServerCertificate=True";

        // Step 2: build the EnrollmentDbContext with the Host assembly as the migration
        // source — EF migrations for all module contexts live in the Host assembly.
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<EnrollmentDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;
        _context = new EnrollmentDbContext(options);

        // Applies pending migrations; a no-op in the red state.
        _context.Database.Migrate();  // Migrate() is synchronous (returns void)

        // Step 3: delete any stale filler rows left by a previous run (idempotent setup).
        DeleteStaleFiller();

        // Step 4: seed fresh filler rows, all scoped by the "AdmPg032E" marker prefix.
        SeedFillerRows();
    }

    public Task DisposeAsync()
    {
        // Best-effort cleanup so a failed run does not leave filler rows behind;
        // setup is idempotent either way.
        try
        {
            DeleteStaleFiller();
        }
        catch
        {
            // Swallowed on purpose: cleanup must never mask the test failure.
        }

        _context?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes stale "AdmPg032E" filler rows (idempotent): enrollments first, then the
    /// filler students and filler courses.
    /// </summary>
    private void DeleteStaleFiller()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using (var enrollmentsCmd = new SqlCommand(
            @"DELETE e FROM Enrollments e
              WHERE EXISTS (SELECT 1 FROM Students s WHERE s.Id = e.StudentId AND s.Name LIKE 'AdmPg032E%')
                 OR EXISTS (SELECT 1 FROM Courses c WHERE c.Id = e.CourseId AND c.Title LIKE 'AdmPg032E%')",
            connection))
        {
            enrollmentsCmd.ExecuteNonQuery();
        }

        using (var studentsCmd = new SqlCommand(
            "DELETE Students WHERE Name LIKE 'AdmPg032E%'", connection))
        {
            studentsCmd.ExecuteNonQuery();
        }

        using (var coursesCmd = new SqlCommand(
            "DELETE Courses WHERE Title LIKE 'AdmPg032E%'", connection))
        {
            coursesCmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Seeds the "AdmPg032E" filler rows via parameterized raw SQL INSERTs:
    /// 12 students, 5 courses, 12 enrollments (student i in course ((i-1) mod 5)+1),
    /// and 1 orphan enrollment referencing a course that does not exist.
    /// </summary>
    private void SeedFillerRows()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        // 12 filler students: "AdmPg032E S01" .. "AdmPg032E S12".
        for (int i = 1; i <= 12; i++)
        {
            var studentId = Guid.NewGuid();
            _studentIds[i - 1] = studentId;

            using var studentCmd = new SqlCommand(
                @"INSERT INTO Students (Id, Name, Email, CreatedAt, PasswordHash, Roles, OrganizationId)
                  VALUES (@Id, @Name, @Email, SYSDATETIMEOFFSET(), @PasswordHash, @Roles, @OrganizationId)",
                connection);
            studentCmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = studentId;
            studentCmd.Parameters.Add("@Name", SqlDbType.NVarChar).Value = $"AdmPg032E S{i:D2}";
            studentCmd.Parameters.Add("@Email", SqlDbType.NVarChar).Value = $"adm.pg032.{i:D2}@example.com";
            studentCmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar).Value = "x";
            studentCmd.Parameters.Add("@Roles", SqlDbType.NVarChar).Value = "Learner";
            studentCmd.Parameters.Add("@OrganizationId", SqlDbType.UniqueIdentifier).Value = RootOrgId;
            studentCmd.ExecuteNonQuery();
        }

        // 5 filler courses: "AdmPg032E Course 1" .. "AdmPg032E Course 5".
        for (int i = 1; i <= 5; i++)
        {
            var courseId = Guid.NewGuid();
            _courseIds[i - 1] = courseId;

            using var courseCmd = new SqlCommand(
                @"INSERT INTO Courses (Id, Title, ShortDescription, FullDescription, Category, Duration, OrganizationId, CreatedAt)
                  VALUES (@Id, @Title, @ShortDescription, @FullDescription, @Category, @Duration, @OrganizationId, SYSDATETIMEOFFSET())",
                connection);
            courseCmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = courseId;
            courseCmd.Parameters.Add("@Title", SqlDbType.NVarChar).Value = $"AdmPg032E Course {i}";
            courseCmd.Parameters.Add("@ShortDescription", SqlDbType.NVarChar).Value = "filler";
            courseCmd.Parameters.Add("@FullDescription", SqlDbType.NVarChar).Value = "filler";
            courseCmd.Parameters.Add("@Category", SqlDbType.NVarChar).Value = "AdmPg032E";
            courseCmd.Parameters.Add("@Duration", SqlDbType.NVarChar).Value = "1 hour";
            courseCmd.Parameters.Add("@OrganizationId", SqlDbType.UniqueIdentifier).Value = RootOrgId;
            courseCmd.ExecuteNonQuery();
        }

        // 12 filler enrollments: student i (1-based) in course ((i-1) mod 5)+1.
        // EnrolledAt decreases as i grows, so the DESC ordering is deterministic.
        var now = DateTimeOffset.UtcNow;
        for (int i = 1; i <= 12; i++)
        {
            var courseIndex = (i - 1) % 5;

            using var enrollmentCmd = new SqlCommand(
                @"INSERT INTO Enrollments (Id, StudentId, CourseId, EnrolledAt)
                  VALUES (@Id, @StudentId, @CourseId, @EnrolledAt)",
                connection);
            enrollmentCmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            enrollmentCmd.Parameters.Add("@StudentId", SqlDbType.UniqueIdentifier).Value = _studentIds[i - 1];
            enrollmentCmd.Parameters.Add("@CourseId", SqlDbType.UniqueIdentifier).Value = _courseIds[courseIndex];
            enrollmentCmd.Parameters.Add("@EnrolledAt", SqlDbType.DateTimeOffset).Value = now.AddSeconds(-i);
            enrollmentCmd.ExecuteNonQuery();
        }

        // 1 orphan enrollment: student S01 in a course that does not exist in Courses.
        // Used to verify the inner join omits enrollments whose course is missing.
        _orphanCourseId = Guid.NewGuid();
        using (var orphanCmd = new SqlCommand(
            @"INSERT INTO Enrollments (Id, StudentId, CourseId, EnrolledAt)
              VALUES (@Id, @StudentId, @CourseId, @EnrolledAt)",
            connection))
        {
            orphanCmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            orphanCmd.Parameters.Add("@StudentId", SqlDbType.UniqueIdentifier).Value = _studentIds[0];
            orphanCmd.Parameters.Add("@CourseId", SqlDbType.UniqueIdentifier).Value = _orphanCourseId;
            orphanCmd.Parameters.Add("@EnrolledAt", SqlDbType.DateTimeOffset).Value = now.AddSeconds(-100);
            orphanCmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Calls the AdminListEnrollments stored procedure and reads both result sets:
    /// result set 1 = the page rows (columns 0..7 in contract order),
    /// result set 2 = the single-row filtered TotalCount.
    /// </summary>
    private async Task<(IList<AdminEnrollmentRowTest> Rows, int TotalCount)> CallSpAsync(
        string? studentName, string? courseTitle, int pageSize, int pageNumber)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = new SqlCommand("AdminListEnrollments", connection);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add("@StudentName", SqlDbType.NVarChar, 200).Value = studentName ?? (object)DBNull.Value;
        cmd.Parameters.Add("@CourseTitle", SqlDbType.NVarChar, 200).Value = courseTitle ?? (object)DBNull.Value;
        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
        cmd.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;

        var rows = new List<AdminEnrollmentRowTest>();
        using var reader = await cmd.ExecuteReaderAsync();

        // Result set 1: the page rows.
        while (await reader.ReadAsync())
        {
            rows.Add(new AdminEnrollmentRowTest(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetGuid(4),
                reader.GetString(5),
                reader.GetGuid(6),
                reader.GetDateTimeOffset(7)));
        }

        // Result set 2: a single row holding the filtered TotalCount.
        var totalCount = 0;
        if (await reader.NextResultAsync())
        {
            if (await reader.ReadAsync())
            {
                totalCount = reader.GetInt32(0);
            }
        }

        return (rows, totalCount);
    }

    /// <summary>
    /// One row of result set 1 from AdminListEnrollments, in contract column order.
    /// </summary>
    private class AdminEnrollmentRowTest
    {
        public AdminEnrollmentRowTest(
            Guid enrollmentId,
            Guid studentId,
            string studentName,
            string studentEmail,
            Guid courseId,
            string courseTitle,
            Guid organizationId,
            DateTimeOffset enrolledAt)
        {
            EnrollmentId = enrollmentId;
            StudentId = studentId;
            StudentName = studentName;
            StudentEmail = studentEmail;
            CourseId = courseId;
            CourseTitle = courseTitle;
            OrganizationId = organizationId;
            EnrolledAt = enrolledAt;
        }

        public Guid EnrollmentId { get; }
        public Guid StudentId { get; }
        public string StudentName { get; }
        public string StudentEmail { get; }
        public Guid CourseId { get; }
        public string CourseTitle { get; }
        public Guid OrganizationId { get; }
        public DateTimeOffset EnrolledAt { get; }
    }

    [Fact]
    public async Task filters_by_student_name()
    {
        var (rows, totalCount) = await CallSpAsync("AdmPg032E S07", null, 10, 1);

        // Only S07's enrollment matches the student name filter.
        Assert.Single(rows);
        Assert.Equal("AdmPg032E S07", rows[0].StudentName);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task filters_by_course_title()
    {
        var (rows, totalCount) = await CallSpAsync(null, "AdmPg032E Course 1", 10, 1);

        // Course 1 holds students S01, S06, and S11.
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal("AdmPg032E Course 1", row.CourseTitle));

        var names = new List<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            names.Add(rows[i].StudentName);
        }
        names.Sort(StringComparer.Ordinal);
        Assert.Equal(new[] { "AdmPg032E S01", "AdmPg032E S06", "AdmPg032E S11" }, names);
        Assert.Equal(3, totalCount);
    }

    [Fact]
    public async Task combines_both_filters()
    {
        var (rows, totalCount) = await CallSpAsync("AdmPg032E S06", "AdmPg032E Course 1", 10, 1);

        // S06 is enrolled in Course 1, so exactly one row matches both filters.
        Assert.Single(rows);
        Assert.Equal("AdmPg032E S06", rows[0].StudentName);
        Assert.Equal("AdmPg032E Course 1", rows[0].CourseTitle);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task paging_math()
    {
        // Scope to the 12 filler enrollments via the marker (the DB holds other,
        // seeded enrollments that a truly unfiltered call would include).
        var (page1, total1) = await CallSpAsync("AdmPg032E", null, 10, 1);
        var (page2, total2) = await CallSpAsync("AdmPg032E", null, 10, 2);
        var (page3, _) = await CallSpAsync("AdmPg032E", null, 10, 3);

        // 12 filler enrollments split across pages of 10: 10 + 2 + 0.
        Assert.Equal(10, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Empty(page3);

        // The filtered total is the same on every page.
        Assert.Equal(12, total1);
        Assert.Equal(12, total2);

        // Pages 1 and 2 together contain all 12 enrollments exactly once.
        var allIds = new List<Guid>();
        for (int i = 0; i < page1.Count; i++)
        {
            allIds.Add(page1[i].EnrollmentId);
        }
        for (int i = 0; i < page2.Count; i++)
        {
            allIds.Add(page2[i].EnrollmentId);
        }
        Assert.Equal(12, allIds.Distinct().Count());
    }

    [Fact]
    public async Task orders_enrolled_at_desc()
    {
        var (rows, _) = await CallSpAsync("AdmPg032E", null, 10, 1);

        // EnrolledAt was seeded strictly decreasing with student index, so the
        // returned page must be strictly decreasing as well.
        for (int i = 1; i < rows.Count; i++)
        {
            Assert.True(rows[i - 1].EnrolledAt > rows[i].EnrolledAt,
                $"Expected EnrolledAt to strictly decrease, but row {i - 1} ({rows[i - 1].EnrolledAt:O}) " +
                $"is not later than row {i} ({rows[i].EnrolledAt:O})");
        }
    }

    [Fact]
    public async Task deterministic_across_calls()
    {
        var first = await CallSpAsync("AdmPg032E", null, 10, 1);
        var second = await CallSpAsync("AdmPg032E", null, 10, 1);

        var firstIds = new List<Guid>();
        for (int i = 0; i < first.Rows.Count; i++)
        {
            firstIds.Add(first.Rows[i].EnrollmentId);
        }

        var secondIds = new List<Guid>();
        for (int i = 0; i < second.Rows.Count; i++)
        {
            secondIds.Add(second.Rows[i].EnrollmentId);
        }

        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public async Task floors_invalid_inputs()
    {
        var (zeroSize, _) = await CallSpAsync("AdmPg032E", null, 0, 1);
        var (negativeSize, _) = await CallSpAsync("AdmPg032E", null, -5, 1);
        var (zeroPage, _) = await CallSpAsync("AdmPg032E", null, 10, 0);
        var (negativePage, _) = await CallSpAsync("AdmPg032E", null, 10, -3);
        var (page1, _) = await CallSpAsync("AdmPg032E", null, 10, 1);

        // pageSize <= 0 floors to 10 rows.
        Assert.Equal(10, zeroSize.Count);
        Assert.Equal(10, negativeSize.Count);

        // pageNumber <= 0 floors to page 1 (same enrollment ids).
        var page1Ids = new List<Guid>();
        for (int i = 0; i < page1.Count; i++)
        {
            page1Ids.Add(page1[i].EnrollmentId);
        }

        var zeroPageIds = new List<Guid>();
        for (int i = 0; i < zeroPage.Count; i++)
        {
            zeroPageIds.Add(zeroPage[i].EnrollmentId);
        }

        var negativePageIds = new List<Guid>();
        for (int i = 0; i < negativePage.Count; i++)
        {
            negativePageIds.Add(negativePage[i].EnrollmentId);
        }

        Assert.Equal(page1Ids, zeroPageIds);
        Assert.Equal(page1Ids, negativePageIds);
    }

    [Fact]
    public async Task empty_string_is_no_filter()
    {
        // An empty string must behave exactly like NULL (no filter).
        var (empty, emptyTotal) = await CallSpAsync("", null, 10, 1);
        var (nullResult, nullTotal) = await CallSpAsync(null, null, 10, 1);

        Assert.Equal(nullTotal, emptyTotal);
        Assert.Equal(
            nullResult.Select(r => r.EnrollmentId).ToList(),
            empty.Select(r => r.EnrollmentId).ToList());
    }

    [Fact]
    public async Task omits_enrollments_with_missing_course()
    {
        var (rows, totalCount) = await CallSpAsync("AdmPg032E", null, 10, 1);

        // The orphan enrollment (course that does not exist) must be dropped by
        // the inner join: the marker-scoped total stays at 12, not 13.
        Assert.Equal(12, totalCount);

        for (int i = 0; i < rows.Count; i++)
        {
            Assert.NotEqual(_orphanCourseId, rows[i].CourseId);
        }
    }
}
