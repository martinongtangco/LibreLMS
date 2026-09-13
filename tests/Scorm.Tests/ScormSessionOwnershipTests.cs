using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Scorm.Tests;

/// <summary>
/// Spec 050 US1 — SCORM session ownership enforcement.
///
/// The four runtime session operations (setValue/getValue/commit/finish) must
/// refuse to act on a session whose stored owner (SessionData.StudentId) is
/// not the caller, before any CMI read/write; the owner's full flow is
/// unchanged; unknown sessions keep the not-found results (no new error
/// surface).
///
/// House pattern (ConcurrentLaunchRetryTests): real MSSQL via
/// ConnectionStrings__Sql, real Valkey via ConnectionStrings__Valkey
/// (host runs use localhost:6380), random-GUID marker students cleaned up
/// afterwards. Sessions are created directly via the store (no launch —
/// the four methods under test do not touch enrollment/catalog/package).
/// </summary>
public class ScormSessionOwnershipTests : IAsyncLifetime
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _intruderId = Guid.NewGuid();
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
        _wwwRoot = Path.Combine(Path.GetTempPath(), $"scorm-050-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_wwwRoot);

        // Clean slate for this run's marker students (random GUIDs — guards
        // against a crashed earlier run).
        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM CourseAttempts WHERE StudentId IN ({0}, {1})", _ownerId, _intruderId);
    }

    public async Task DisposeAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM CourseAttempts WHERE StudentId IN ({0}, {1})", _ownerId, _intruderId);
        _mux.Close();
        _mux.Dispose();
        if (Directory.Exists(_wwwRoot))
            Directory.Delete(_wwwRoot, recursive: true);
    }

    [Fact]
    public async Task Intruder_allFourOperations_refused_andOwnerStateUntouched()
    {
        var (sessionId, attemptId, store, service) = await PrepareOwnerSessionAsync();

        // setValue: refused, and the value must NOT be written
        var set = await service.SetValueAsync(sessionId, _intruderId, "cmi.core.lesson_status", "passed");
        Assert.True(set.Forbidden, "an intruder's setValue must be forbidden");
        Assert.False(set.Success);

        // getValue: refused — no CMI data leaks to a non-owner
        var get = await service.GetValueAsync(sessionId, _intruderId, "cmi.core.lesson_status");
        Assert.True(get.Forbidden, "an intruder's getValue must be forbidden");
        Assert.False(get.Found);

        // commit: refused — the attempt must not be mutated
        var commit = await service.CommitAsync(sessionId, _intruderId);
        Assert.True(commit.Forbidden, "an intruder's commit must be forbidden");
        Assert.False(commit.Success);

        // finish: refused — the attempt must not be completed, the session
        // must not be deleted
        var finish = await service.FinishAsync(sessionId, _intruderId);
        Assert.True(finish.Forbidden, "an intruder's finish must be forbidden");
        Assert.False(finish.Success);

        // Owner state intact: session still exists with default values
        var read = await store.ReadSessionAsync(sessionId);
        Assert.True(read is not null, "the session must still exist after an intruder's refused calls");
        Assert.True(read!.CmiLessonStatus == "not attempted", "the intruder's setValue must not have been stored");

        // Attempt untouched
        await using var ctx = NewContext();
        var attempt = await ctx.CourseAttempts.FindAsync(attemptId);
        Assert.NotNull(attempt);
        Assert.True(attempt!.Status == "in-progress", "the intruder's commit/finish must not have updated the attempt");
        Assert.True(attempt.CompletedAt is null, "the intruder's finish must not have completed the attempt");
    }

    [Fact]
    public async Task Owner_fullFlow_succeeds()
    {
        var (sessionId, attemptId, store, service) = await PrepareOwnerSessionAsync();

        var set = await service.SetValueAsync(sessionId, _ownerId, "cmi.core.lesson_status", "passed");
        Assert.True(set.Success, $"owner setValue must succeed: {set.ErrorMsg}");
        Assert.False(set.Forbidden);

        var get = await service.GetValueAsync(sessionId, _ownerId, "cmi.core.lesson_status");
        Assert.True(get.Found);
        Assert.Equal("passed", get.Value);

        var commit = await service.CommitAsync(sessionId, _ownerId);
        Assert.True(commit.Success, $"owner commit must succeed: {commit.Error}");

        var finish = await service.FinishAsync(sessionId, _ownerId, "normal");
        Assert.True(finish.Success, $"owner finish must succeed: {finish.Error}");
        Assert.Equal("passed", finish.Status);

        // Attempt completed, session cleaned up
        await using var ctx = NewContext();
        var attempt = await ctx.CourseAttempts.FindAsync(attemptId);
        Assert.NotNull(attempt);
        Assert.Equal("passed", attempt!.Status);
        Assert.NotNull(attempt.CompletedAt);
        Assert.True(await store.ReadSessionAsync(sessionId) is null, "finish must delete the session");
    }

    [Fact]
    public async Task UnknownSession_returnsNotFound_notForbidden()
    {
        var service = NewService();
        var randomSessionId = Guid.NewGuid();

        var set = await service.SetValueAsync(randomSessionId, _ownerId, "cmi.core.lesson_status", "passed");
        Assert.False(set.Forbidden);
        Assert.False(set.Success);
        Assert.True(set.ErrorCode == "404", $"unknown sessions keep the existing not-found error code (got {set.ErrorCode})");

        var get = await service.GetValueAsync(randomSessionId, _ownerId, "cmi.core.lesson_status");
        Assert.False(get.Forbidden);
        Assert.False(get.Found);

        var commit = await service.CommitAsync(randomSessionId, _ownerId);
        Assert.False(commit.Forbidden);
        Assert.False(commit.Success);

        var finish = await service.FinishAsync(randomSessionId, _ownerId);
        Assert.False(finish.Forbidden);
        Assert.False(finish.Success);
    }

    // ── Helpers ──

    private async Task<(Guid sessionId, Guid attemptId, ScormSessionStore store, ScormSessionService service)> PrepareOwnerSessionAsync()
    {
        var sessionId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        await using (var ctx = NewContext())
        {
            SeedAttempt(ctx, _ownerId, attemptId);
            await ctx.SaveChangesAsync();
        }

        var store = new ScormSessionStore(_mux);
        await store.CreateSessionAsync(SessionData.CreateDefault(sessionId, _ownerId, _courseId, attemptId, "initial"));

        return (sessionId, attemptId, store, NewService());
    }

    private ScormSessionService NewService() => new(
        NewContext(),
        new ScormSessionStore(_mux),
        NotEnrolledLookup.Instance,
        new ScormPackageService(NewContext(), new ManifestParser(), _wwwRoot));

    private ScormDbContext NewContext()
    {
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<ScormDbContext>()
            .UseSqlServer(_sqlConn, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;
        return new ScormDbContext(options);
    }

    private void SeedAttempt(ScormDbContext ctx, Guid studentId, Guid attemptId)
    {
        ctx.CourseAttempts.Add(new CourseAttempt
        {
            Id = attemptId,
            StudentId = studentId,
            CourseId = _courseId,
            AttemptNumber = 1,
            Status = "in-progress",
            StartedAt = DateTimeOffset.UtcNow,
            LastCommitAt = DateTimeOffset.UtcNow
        });
    }

    private sealed class NotEnrolledLookup : IEnrollmentLookup
    {
        public static NotEnrolledLookup Instance { get; } = new();
        public Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Guid>> GetEnrolledCourseIdsAsync(Guid studentId, IEnumerable<Guid> courseIds)
            => Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
    }
}
