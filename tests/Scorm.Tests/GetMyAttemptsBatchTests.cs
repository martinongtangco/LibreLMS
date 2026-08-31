using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Scorm.Tests;

/// <summary>
/// Spec 048 (E5) — counting-fake test at the SERVICE level:
/// <c>ScormAttemptService.GetMyAttemptsAsync</c> must resolve course titles for
/// N attempts with exactly ONE <c>ICourseLookup.GetCoursesAsync</c> call and zero
/// per-row <c>GetCourseAsync</c> calls.
///
/// Follows the project's Scorm.Tests pattern (ConcurrentLaunchRetryTests): real
/// MSSQL via ConnectionStrings__Sql (migrations live in the Host assembly) and a
/// random-GUID marker student that is cleaned up afterwards.
/// </summary>
public class GetMyAttemptsBatchTests : IAsyncLifetime
{
    private readonly Guid _studentId = Guid.NewGuid();
    private string _sqlConn = null!;

    public async Task InitializeAsync()
    {
        _sqlConn = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? throw new InvalidOperationException("ConnectionStrings__Sql environment variable is required.");

        // Clean slate for this run's marker student (random GUID — guards against a crashed earlier run).
        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM CourseAttempts WHERE StudentId = {0}", _studentId);
    }

    public async Task DisposeAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM CourseAttempts WHERE StudentId = {0}", _studentId);
    }

    [Fact]
    public async Task GetMyAttemptsAsync_UsesSingleBatchLookup_ForNAttempts()
    {
        var courseLookup = new CountingCourseLookup();
        var course1 = courseLookup.AddCourse("SCORM One");
        var course2 = courseLookup.AddCourse("SCORM Two");
        var missingCourse = Guid.NewGuid(); // deliberately absent from the fake catalog

        await using var ctx = NewContext();
        SeedAttempt(ctx, course1);
        SeedAttempt(ctx, course2);
        SeedAttempt(ctx, missingCourse);
        await ctx.SaveChangesAsync();

        var service = new ScormAttemptService(ctx, courseLookup);
        var summaries = (await service.GetMyAttemptsAsync(_studentId)).ToList();

        Assert.Equal(3, summaries.Count);
        Assert.Equal(1, courseLookup.GetCoursesCalls);
        Assert.Equal(0, courseLookup.GetCourseCalls);
        Assert.Equal(
            new[] { course1, course2, missingCourse }.ToHashSet(),
            courseLookup.LastBatchedIds!.ToHashSet());

        // Titles resolved from the single batch; missing course keeps the fallback.
        Assert.Contains(summaries, s => s.CourseTitle == "SCORM One");
        Assert.Contains(summaries, s => s.CourseTitle == "SCORM Two");
        Assert.Contains(summaries, s => s.CourseTitle == "Unknown Course");
    }

    [Fact]
    public async Task GetMyAttemptsAsync_NoAttempts_MakesNoLookupCall()
    {
        var courseLookup = new CountingCourseLookup();

        await using var ctx = NewContext();
        var service = new ScormAttemptService(ctx, courseLookup);
        var summaries = await service.GetMyAttemptsAsync(_studentId);

        Assert.Empty(summaries);
        Assert.Equal(0, courseLookup.GetCoursesCalls);
        Assert.Equal(0, courseLookup.GetCourseCalls);
    }

    // ── Helpers ──

    private ScormDbContext NewContext()
    {
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<ScormDbContext>()
            .UseSqlServer(_sqlConn, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;
        return new ScormDbContext(options);
    }

    private void SeedAttempt(ScormDbContext ctx, Guid courseId)
    {
        ctx.CourseAttempts.Add(new CourseAttempt
        {
            StudentId = _studentId,
            CourseId = courseId,
            AttemptNumber = 1,
            Status = "in-progress",
            StartedAt = DateTimeOffset.UtcNow,
            LastCommitAt = DateTimeOffset.UtcNow
        });
    }
}

/// <summary>ICourseLookup fake that counts per-row vs. batched lookup calls (spec 048).</summary>
public class CountingCourseLookup : ICourseLookup
{
    private readonly Dictionary<Guid, CourseSummary> _courses = new();

    public int GetCourseCalls { get; private set; }
    public int GetCoursesCalls { get; private set; }
    public List<Guid>? LastBatchedIds { get; private set; }

    public Guid AddCourse(string title)
    {
        var id = Guid.NewGuid();
        _courses[id] = new CourseSummary(id, title, "General", Guid.NewGuid());
        return id;
    }

    public Task<CourseSummary?> GetCourseAsync(Guid courseId)
    {
        GetCourseCalls++;
        _courses.TryGetValue(courseId, out var course);
        return Task.FromResult(course);
    }

    public Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds)
    {
        GetCoursesCalls++;
        var ids = courseIds.ToList();
        LastBatchedIds = ids;
        return Task.FromResult<IList<CourseSummary>>(
            ids.Where(_courses.ContainsKey).Select(id => _courses[id]).ToList());
    }

    // Unused by the tests — contract completeness.
    public Task<int> CountAsync() => Task.FromResult(_courses.Count);
    public Task<int> CountByOrgAsync(Guid organizationId) => Task.FromResult(0);
    public Task<IReadOnlyDictionary<Guid, int>> GetCourseCountsByOrgsAsync(IEnumerable<Guid> organizationIds)
        => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    public Task<IList<string>> GetDistinctCategoriesAsync()
        => Task.FromResult<IList<string>>(Array.Empty<string>());
    public Task<IList<CourseSummary>> ListByOrgsAsync(IEnumerable<Guid> organizationIds)
        => Task.FromResult<IList<CourseSummary>>(Array.Empty<CourseSummary>());
    public Task<IList<CourseSummary>> ListAllAsync()
        => Task.FromResult<IList<CourseSummary>>(_courses.Values.ToList());
}
