using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Scorm.Tests;

/// <summary>
/// Concurrent-launch regression test (spec 046): N parallel
/// <c>ScormSessionService.LaunchAsync</c> calls for the same student/course
/// with no pre-existing active session — the exact interleaving that makes
/// the non-atomic max+1 attempt numbering collide (spec 044's race).
///
/// With spec 044's original catch (raw <c>SqlException</c>), the losing
/// inserts escape as unhandled <c>DbUpdateException</c>s and this test fails.
/// With the fixed catch (<c>DbUpdateException</c> + inner <c>SqlException</c>
/// 2601), every call completes: at least one launch succeeds, the rest get a
/// clean "session already active" / conflict result, and no duplicate
/// attempt numbers exist afterwards.
///
/// Mirrors production scoping: each parallel task gets its OWN
/// ScormDbContext (DbContext is not thread-safe; requests are scoped in
/// ASP.NET Core). The Valkey session store is real (shared
/// IConnectionMultiplexer is thread-safe).
///
/// Requires running MSSQL (ConnectionStrings__Sql) and Valkey
/// (ConnectionStrings__Valkey; host runs use localhost:6380).
/// </summary>
public class ConcurrentLaunchRetryTests : IAsyncLifetime
{
    /// <summary>Parallel launches — wide enough that the race window is hit on essentially every run.</summary>
    private const int ParallelLaunches = 16;

    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private string _sqlConn = null!;
    private IConnectionMultiplexer _mux = null!;
    private string _wwwRoot = null!;

    public async Task InitializeAsync()
    {
        _sqlConn = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? throw new InvalidOperationException("ConnectionStrings__Sql environment variable is required.");
        var valkeyConn = Environment.GetEnvironmentVariable("ConnectionStrings__Valkey") ?? "localhost:6380";
        _mux = await ConnectionMultiplexer.ConnectAsync(valkeyConn);

        _wwwRoot = Path.Combine(Path.GetTempPath(), $"scorm-046-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_wwwRoot);

        // Clean slate for this run's marker student (random GUID — guards against a crashed earlier run).
        await using (var ctx = NewContext())
        {
            await ctx.Database.MigrateAsync();
            ctx.Database.ExecuteSqlRaw("DELETE FROM CourseAttempts WHERE StudentId = {0}", _studentId);
        }

        // Drop any stale session keys for the marker pair.
        await DeleteMarkerSessionsAsync();
    }

    public async Task DisposeAsync()
    {
        await DeleteMarkerSessionsAsync();
        await using var ctx = NewContext();
        ctx.Database.ExecuteSqlRaw("DELETE FROM CourseAttempts WHERE StudentId = {0}", _studentId);
        _mux.Close();
        _mux.Dispose();
        if (Directory.Exists(_wwwRoot))
            Directory.Delete(_wwwRoot, recursive: true);
    }

    [Fact]
    public async Task Parallel_launches_never_throw_and_number_attempts_uniquely()
    {
        var tasks = Enumerable.Range(0, ParallelLaunches).Select(_ => Task.Run(async () =>
        {
            // Per-task scope, exactly like ASP.NET Core request scoping.
            await using var ctx = NewContext();
            var service = new ScormSessionService(
                ctx,
                new ScormSessionStore(_mux),
                AlwaysEnrolledLookup.Instance,
                new ScormPackageService(ctx, new ManifestParser(), _wwwRoot));
            return await service.LaunchAsync(_studentId, _courseId);
        }));

        // Must complete WITHOUT any task throwing (spec 044's symptom was an
        // unhandled duplicate-key exception escaping the launch endpoint).
        var results = await Task.WhenAll(tasks);

        Assert.Equal(ParallelLaunches, results.Length);

        var successes = results.Count(r => r.Success);
        Assert.True(successes >= 1, $"expected at least one successful launch, got none; errors: {string.Join(" | ", results.Where(r => !r.Success).Select(r => r.Error))}");

        // Every non-success must be a CLEAN business outcome (already-active
        // or conflict) — never an exception, never null.
        foreach (var r in results.Where(r => !r.Success))
            Assert.False(string.IsNullOrEmpty(r.Error), "a failed launch must carry a clean error message");

        // Attempt numbers: unique, consecutive from 1 (the retry re-reads the
        // fresh max, so no gaps in this scenario).
        await using var check = NewContext();
        var numbers = await check.CourseAttempts
            .Where(a => a.StudentId == _studentId && a.CourseId == _courseId)
            .Select(a => a.AttemptNumber)
            .ToListAsync();

        Assert.True(numbers.Count >= 1, "at least one CourseAttempt row must exist");
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, numbers.Count), numbers.OrderBy(n => n));
    }

    private ScormDbContext NewContext()
    {
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<ScormDbContext>()
            .UseSqlServer(_sqlConn, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;
        return new ScormDbContext(options);
    }

    /// <summary>
    /// Delete every Valkey session key whose stored studentId is this test's
    /// marker (session keys carry the student id inside the hash).
    /// </summary>
    private async Task DeleteMarkerSessionsAsync()
    {
        var store = new ScormSessionStore(_mux);
        foreach (var endPoint in _mux.GetEndPoints())
        {
            var server = _mux.GetServer(endPoint);
            if (!server.IsConnected)
                continue;

            foreach (var key in server.Keys(pattern: "scorm:session:*"))
            {
                var rawKey = key.ToString();
                if (!Guid.TryParse(rawKey["scorm:session:".Length..], out var sid))
                    continue;
                var session = await store.ReadSessionAsync(sid);
                if (session is not null && session.StudentId == _studentId.ToString())
                    await store.DeleteSessionAsync(sid);
            }
        }
    }

    /// <summary>The enrollment check is orthogonal to the race — always enrolled.</summary>
    private sealed class AlwaysEnrolledLookup : IEnrollmentLookup
    {
        public static readonly AlwaysEnrolledLookup Instance = new();
        public Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId) => Task.FromResult(true);
    }
}
