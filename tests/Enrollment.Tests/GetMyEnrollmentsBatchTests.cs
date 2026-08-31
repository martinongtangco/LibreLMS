using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Enrollment.Tests;

/// <summary>
/// Spec 048 (E4) — counting-fake test at the SERVICE level:
/// <c>EnrollmentService.GetMyEnrollmentsAsync</c> must resolve course titles for
/// N enrollments with exactly ONE <c>ICourseLookup.GetCoursesAsync</c> call and
/// zero per-row <c>GetCourseAsync</c> calls. The DB side is EF InMemory (same
/// pattern as EnrollmentServiceTests).
/// </summary>
public class GetMyEnrollmentsBatchTests : IDisposable
{
    private readonly EnrollmentDbContext _context;
    private readonly CountingCourseLookup _courseLookup;
    private readonly EnrollmentService _service;

    public GetMyEnrollmentsBatchTests()
    {
        var options = new DbContextOptionsBuilder<EnrollmentDbContext>()
            .UseInMemoryDatabase(databaseName: $"GetMyEnrollmentsTests_{Guid.NewGuid()}")
            .Options;

        _context = new EnrollmentDbContext(options);
        _courseLookup = new CountingCourseLookup();
        _service = new EnrollmentService(_context, _courseLookup);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetMyEnrollmentsAsync_UsesSingleBatchLookup_ForNEnrollments()
    {
        var student = CreateStudent();
        var course1 = _courseLookup.AddCourse("Course One");
        var course2 = _courseLookup.AddCourse("Course Two");
        var course3 = _courseLookup.AddCourse("Course Three");
        var missingCourse = Guid.NewGuid(); // deliberately absent from the fake catalog

        SeedEnrollment(student.Id, course1);
        SeedEnrollment(student.Id, course2);
        SeedEnrollment(student.Id, course3);
        SeedEnrollment(student.Id, missingCourse);
        await _context.SaveChangesAsync();

        var results = await _service.GetMyEnrollmentsAsync(student.Id);

        Assert.Equal(4, results.Count());
        Assert.Equal(1, _courseLookup.GetCoursesCalls);
        Assert.Equal(0, _courseLookup.GetCourseCalls);
        // The batch call must have carried every enrolled course id exactly once.
        Assert.Equal(
            new[] { course1, course2, course3, missingCourse }.ToHashSet(),
            _courseLookup.LastBatchedIds!.ToHashSet());
    }

    [Fact]
    public async Task GetMyEnrollmentsAsync_KeepsTitlesAndUnknownCourseFallback()
    {
        var student = CreateStudent();
        var known = _courseLookup.AddCourse("Known Course");
        var missing = Guid.NewGuid();

        SeedEnrollment(student.Id, known);
        SeedEnrollment(student.Id, missing);
        await _context.SaveChangesAsync();

        var results = (await _service.GetMyEnrollmentsAsync(student.Id)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Item2 == "Known Course");
        Assert.Contains(results, r => r.Item2 == "Unknown Course");
    }

    [Fact]
    public async Task GetMyEnrollmentsAsync_NoEnrollments_MakesNoLookupCall()
    {
        var student = CreateStudent();

        var results = await _service.GetMyEnrollmentsAsync(student.Id);

        Assert.Empty(results);
        Assert.Equal(0, _courseLookup.GetCoursesCalls);
        Assert.Equal(0, _courseLookup.GetCourseCalls);
    }

    // ── Helpers ──

    private static Student CreateStudent()
    {
        return new Student
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = $"test{Guid.NewGuid():N}@example.com",
            PasswordHash = "hashed",
            Roles = "Learner",
            OrganizationId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SeedEnrollment(Guid studentId, Guid courseId)
    {
        _context.Enrollments.Add(new LibreLms.Modules.Enrollment.Domain.Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            EnrolledAt = DateTimeOffset.UtcNow
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
