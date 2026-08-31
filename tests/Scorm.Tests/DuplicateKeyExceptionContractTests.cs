using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;

namespace Scorm.Tests;

/// <summary>
/// Exception-contract regression test (spec 046): pins down what exception
/// type escapes <c>SaveChangesAsync()</c> when the unique index
/// IX_CourseAttempts_StudentId_CourseId_AttemptNumber rejects a duplicate
/// (student, course, attempt) insert. EF Core wraps the provider's
/// <c>SqlException</c> (number 2601) in a <c>DbUpdateException</c> — the
/// top-level exception is NOT a raw <c>SqlException</c>. The retry loop in
/// <c>ScormSessionService.LaunchAsync</c> relies on this contract (spec 044
/// originally caught the raw <c>SqlException</c>, which never fires — bug-046).
/// If EF Core ever changes its wrapping behavior, this test fails loudly so
/// the catch in the service is revisited.
///
/// Requires a running MSSQL instance; connection string from the
/// ConnectionStrings__Sql environment variable (host runs use
/// Server=localhost,1433;Database=LibreLms;...).
/// </summary>
public class DuplicateKeyExceptionContractTests : IAsyncLifetime
{
    private ScormDbContext _ctx = null!;

    public async Task InitializeAsync()
    {
        var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? throw new InvalidOperationException("ConnectionStrings__Sql environment variable is required.");

        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<ScormDbContext>()
            .UseSqlServer(connStr, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;

        _ctx = new ScormDbContext(options);
        _ctx.Database.Migrate();
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Duplicate_attempt_insert_reports_exception_type()
    {
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _ctx.CourseAttempts.Add(new CourseAttempt
        {
            StudentId = studentId,
            CourseId = courseId,
            AttemptNumber = 1,
            Status = "in-progress",
            StartedAt = now,
            LastCommitAt = now
        });
        _ctx.SaveChanges();

        // The racing second insert: identical (student, course, attempt number).
        _ctx.CourseAttempts.Add(new CourseAttempt
        {
            StudentId = studentId,
            CourseId = courseId,
            AttemptNumber = 1,
            Status = "in-progress",
            StartedAt = now,
            LastCommitAt = now
        });

        string topType = "<none>", innerType = "<none>";
        int? sqlNumber = null;
        bool topIsRawSqlException = false;
        try
        {
            _ctx.SaveChanges();
            throw new InvalidOperationException("Contract test FAILED: no exception thrown on duplicate key");
        }
        catch (Exception ex)
        {
            topType = ex.GetType().FullName ?? "<unknown>";
            innerType = ex.InnerException?.GetType().FullName ?? "<null>";
            if (ex.InnerException is SqlException sql)
                sqlNumber = sql.Number;
            topIsRawSqlException = ex is SqlException;
            Console.WriteLine($"CONTRACT top-type:    {topType}");
            Console.WriteLine($"CONTRACT inner-type:  {innerType}");
            Console.WriteLine($"CONTRACT sql-number:  {sqlNumber}");
            Console.WriteLine($"CONTRACT raw-SqlException-catch-would-fire: {topIsRawSqlException}");
        }
        finally
        {
            _ctx.ChangeTracker.Clear();
            _ctx.Database.ExecuteSqlRaw("DELETE FROM CourseAttempts WHERE StudentId = {0}", studentId);
        }

        // Pin the observed contract (asserted so a re-run fails loudly if EF changes behavior).
        Assert.Equal("Microsoft.EntityFrameworkCore.DbUpdateException", topType);
        Assert.Equal(typeof(SqlException).FullName, innerType);
        Assert.Equal(2601, sqlNumber);
        Assert.False(topIsRawSqlException);
    }
}
