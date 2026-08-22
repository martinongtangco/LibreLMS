using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Enrollment.Infrastructure;
using Xunit;

namespace Enrollment.Tests;

/// <summary>
/// Integration tests for the dbo.AdminListLearners stored procedure (spec 032, task T016).
/// The procedure does not exist in the database yet — these tests are the TDD red state
/// and are expected to fail at runtime until the EF migration that creates the procedure
/// lands in the Host assembly.
/// All assertions are scoped to the 'AdmPg032L%' filler marker prefix; other rows in the
/// database (seeded users, etc.) are irrelevant.
/// Tests require a running MSSQL instance (docker compose up mssql).
/// </summary>
public class AdminListLearnersTests : IAsyncLifetime
{
    private string _connectionString;
    private EnrollmentDbContext _context;

    // Ids of the 12 filler students created by InitializeAsync, in insertion order.
    private Guid _studentId01;
    private Guid _studentId02;
    private Guid _studentId03;
    private Guid _studentId04;
    private Guid _studentId05;
    private Guid _studentId06;
    private Guid _studentId07;
    private Guid _studentId08;
    private Guid _studentId09;
    private Guid _studentId10;
    private Guid _studentId11;
    private Guid _studentId12;

    // FieldCount of result set 1 from the most recent CallSpAsync call. Exposed here so the
    // credential-column test can assert on the raw reader shape before any row is consumed.
    private int _lastResultFieldCount;

    public async Task InitializeAsync()
    {
        // Build connection string from environment or use the default (DB host is 'mssql', not localhost).
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? "Server=mssql,1433;Database=LearningLms;User Id=sa;Password=Lms#vZdV361xAfdYmoEZmTmh!9;TrustServerCertificate=True";

        // The EF migrations live in the Host assembly, so it must be used as the migration source.
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<EnrollmentDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;

        _context = new EnrollmentDbContext(options);

        // Applies the Enrollment migrations. No-op in the red state (procedure migration not yet present).
        _context.Database.Migrate();  // Migrate() is synchronous (returns void)

        // Delete stale filler from any previous run so seeding is idempotent.
        await ExecuteRawSqlAsync("DELETE FROM Students WHERE Name LIKE 'AdmPg032L%'");

        // Generate all 12 filler student ids up front and keep them in order.
        _studentId01 = Guid.NewGuid();
        _studentId02 = Guid.NewGuid();
        _studentId03 = Guid.NewGuid();
        _studentId04 = Guid.NewGuid();
        _studentId05 = Guid.NewGuid();
        _studentId06 = Guid.NewGuid();
        _studentId07 = Guid.NewGuid();
        _studentId08 = Guid.NewGuid();
        _studentId09 = Guid.NewGuid();
        _studentId10 = Guid.NewGuid();
        _studentId11 = Guid.NewGuid();
        _studentId12 = Guid.NewGuid();

        var ids = new[]
        {
            _studentId01, _studentId02, _studentId03, _studentId04, _studentId05, _studentId06,
            _studentId07, _studentId08, _studentId09, _studentId10, _studentId11, _studentId12
        };

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(
            "INSERT INTO Students (Id, Name, Email, CreatedAt, PasswordHash, Roles, OrganizationId) " +
            "VALUES (@Id, @Name, @Email, SYSDATETIMEOFFSET(), @PasswordHash, @Roles, @OrganizationId)",
            connection);
        var idParam = command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier);
        var nameParam = command.Parameters.Add("@Name", SqlDbType.NVarChar, 200);
        var emailParam = command.Parameters.Add("@Email", SqlDbType.NVarChar, 320);
        var passwordHashParam = command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar);
        var rolesParam = command.Parameters.Add("@Roles", SqlDbType.NVarChar, 50);
        var organizationIdParam = command.Parameters.Add("@OrganizationId", SqlDbType.UniqueIdentifier);
        organizationIdParam.Value = Guid.Parse("00000000-0000-0000-0000-000000000001");

        for (int i = 1; i <= 12; i++)
        {
            var name = $"AdmPg032L Alpha{i:D2}";
            var email = $"adm.pg032l.{i:D2}@example.com";

            // Roles rotate by position (1-based): 1 -> Learner, 2 -> OrgAdmin, 0 -> SuperUser (4 of each).
            string roles;
            if (i % 3 == 1)
            {
                roles = "Learner";
            }
            else if (i % 3 == 2)
            {
                roles = "OrgAdmin";
            }
            else
            {
                roles = "SuperUser";
            }

            idParam.Value = ids[i - 1];
            nameParam.Value = name;
            emailParam.Value = email;
            passwordHashParam.Value = "x";
            rolesParam.Value = roles;

            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        // Remove filler rows so the database is left as we found it, then dispose the context.
        try
        {
            await ExecuteRawSqlAsync("DELETE FROM Students WHERE Name LIKE 'AdmPg032L%'");
        }
        finally
        {
            _context?.Dispose();
        }
    }

    private async Task ExecuteRawSqlAsync(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Calls dbo.AdminListLearners and returns result set 1 (the page rows) plus result set 2 (TotalCount).
    private async Task<(IList<AdminLearnerRowTest> Rows, int TotalCount)> CallSpAsync(
        string? search,
        string? role,
        int pageSize,
        int pageNumber)
    {
        var rows = new List<AdminLearnerRowTest>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand("AdminListLearners", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = search ?? (object)DBNull.Value;
        command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = role ?? (object)DBNull.Value;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
        command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;

        using var reader = await command.ExecuteReaderAsync();

        // Capture the shape of result set 1 before any row is read (used by the credential-column test).
        _lastResultFieldCount = reader.FieldCount;

        // Result set 1: the page of learner rows, columns 0..7.
        while (await reader.ReadAsync())
        {
            rows.Add(new AdminLearnerRowTest(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetGuid(4),
                reader.GetDateTimeOffset(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        // Result set 2: a single row holding the filtered TotalCount.
        int totalCount = 0;
        if (await reader.NextResultAsync())
        {
            if (await reader.ReadAsync())
            {
                totalCount = reader.GetInt32(0);
            }
        }

        return (rows, totalCount);
    }

    // One row of AdminListLearners result set 1, in the documented column order.
    private sealed record AdminLearnerRowTest(
        Guid Id,
        string Name,
        string Email,
        string Roles,
        Guid OrganizationId,
        DateTimeOffset CreatedAt,
        bool IsEmailVerified,
        string? AvatarPath);

    // ===== Tests =====

    [Fact]
    public async Task name_search_matches()
    {
        var (rows, totalCount) = await CallSpAsync("AdmPg032L Alpha0", null, 10, 1);

        // Alpha01..Alpha09 contain "AdmPg032L Alpha0"; Alpha10..Alpha12 do not.
        Assert.Equal(9, rows.Count);
        Assert.All(rows, row => Assert.Contains("AdmPg032L Alpha0", row.Name));
        Assert.Equal(9, totalCount);
    }

    [Fact]
    public async Task email_search_matches()
    {
        var (rows, totalCount) = await CallSpAsync("adm.pg032l.05", null, 10, 1);

        Assert.Equal(1, rows.Count);
        Assert.Equal("AdmPg032L Alpha05", rows[0].Name);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task search_hits_name_or_email()
    {
        var (rows, totalCount) = await CallSpAsync("AdmPg032L Alpha1", null, 10, 1);

        // Only Alpha10, Alpha11 and Alpha12 contain "AdmPg032L Alpha1".
        Assert.Equal(3, rows.Count);

        var names = rows.Select(row => row.Name).ToList();
        Assert.Contains("AdmPg032L Alpha10", names);
        Assert.Contains("AdmPg032L Alpha11", names);
        Assert.Contains("AdmPg032L Alpha12", names);

        Assert.Equal(3, totalCount);
    }

    [Fact]
    public async Task exact_role_filter()
    {
        // Scope to the 12 filler students via the marker (the DB holds other,
        // seeded accounts that an unfiltered role query would include).
        var (rows, totalCount) = await CallSpAsync("AdmPg032L", "OrgAdmin", 10, 1);

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal("OrgAdmin", row.Roles));
        Assert.Equal(4, totalCount);
    }

    [Fact]
    public async Task search_plus_role_combined()
    {
        var (rows, totalCount) = await CallSpAsync("AdmPg032L Alpha", "SuperUser", 10, 1);

        // SuperUser rows are positions 3, 6, 9 and 12.
        Assert.Equal(4, rows.Count);

        var names = rows.Select(row => row.Name).ToList();
        Assert.Contains("AdmPg032L Alpha03", names);
        Assert.Contains("AdmPg032L Alpha06", names);
        Assert.Contains("AdmPg032L Alpha09", names);
        Assert.Contains("AdmPg032L Alpha12", names);

        Assert.Equal(4, totalCount);
    }

    [Fact]
    public async Task paging_math()
    {
        var (page1, totalCount1) = await CallSpAsync("AdmPg032L", null, 10, 1);
        var (page2, totalCount2) = await CallSpAsync("AdmPg032L", null, 10, 2);
        var (page3, _) = await CallSpAsync("AdmPg032L", null, 10, 3);

        Assert.Equal(10, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Empty(page3);

        // The two non-empty pages together must cover all 12 filler students exactly once.
        var allIds = new List<Guid>();
        allIds.AddRange(page1.Select(row => row.Id));
        allIds.AddRange(page2.Select(row => row.Id));
        Assert.Equal(12, allIds.Distinct().Count());

        Assert.Equal(12, totalCount1);
        Assert.Equal(12, totalCount2);
    }

    [Fact]
    public async Task orders_name_asc()
    {
        var (rows, _) = await CallSpAsync("AdmPg032L", null, 10, 1);

        // Names must be strictly ascending from row to row.
        for (int i = 1; i < rows.Count; i++)
        {
            Assert.True(
                string.Compare(rows[i - 1].Name, rows[i].Name, StringComparison.Ordinal) < 0,
                $"Expected ascending names, but '{rows[i - 1].Name}' is not before '{rows[i].Name}'");
        }
    }

    [Fact]
    public async Task deterministic_across_calls()
    {
        var (first, _) = await CallSpAsync("AdmPg032L", null, 10, 1);
        var (second, _) = await CallSpAsync("AdmPg032L", null, 10, 1);

        var firstIds = first.Select(row => row.Id).ToList();
        var secondIds = second.Select(row => row.Id).ToList();

        // Identical calls must return identical Id sequences.
        for (int i = 0; i < firstIds.Count; i++)
        {
            Assert.Equal(firstIds[i], secondIds[i]);
        }
    }

    [Fact]
    public async Task floors_invalid_inputs()
    {
        var (baseline, _) = await CallSpAsync("AdmPg032L", null, 10, 1);
        var (zeroPageSize, _) = await CallSpAsync("AdmPg032L", null, 0, 1);
        var (negativePageSize, _) = await CallSpAsync("AdmPg032L", null, -5, 1);
        var (zeroPageNumber, _) = await CallSpAsync("AdmPg032L", null, 10, 0);
        var (negativePageNumber, _) = await CallSpAsync("AdmPg032L", null, 10, -3);

        // Invalid page sizes floor to 10.
        Assert.Equal(10, zeroPageSize.Count);
        Assert.Equal(10, negativePageSize.Count);

        // Invalid page numbers floor to page 1.
        var baselineIds = baseline.Select(row => row.Id).ToList();
        Assert.Equal(baselineIds, zeroPageNumber.Select(row => row.Id).ToList());
        Assert.Equal(baselineIds, negativePageNumber.Select(row => row.Id).ToList());
    }

    [Fact]
    public async Task empty_search_is_no_filter()
    {
        // An empty search string must behave exactly like NULL (no filter).
        var (emptyRows, emptyTotal) = await CallSpAsync("", null, 10, 1);
        var (nullRows, nullTotal) = await CallSpAsync(null, null, 10, 1);

        Assert.Equal(nullTotal, emptyTotal);
        Assert.Equal(nullRows.Select(row => row.Id).ToList(), emptyRows.Select(row => row.Id).ToList());
    }

    [Fact]
    public async Task never_exposes_credential_columns()
    {
        var (_, _) = await CallSpAsync(null, null, 10, 1);

        // Result set 1 must contain exactly the 8 documented columns. PasswordHash and
        // SecurityStamp are deliberately never part of the listing.
        Assert.Equal(8, _lastResultFieldCount);
    }
}
