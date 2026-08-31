using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace Enrollment.Tests;

/// <summary>Unit tests for EnrollmentService methods added in spec 017.</summary>
public class EnrollmentServiceTests : IDisposable
{
    private readonly EnrollmentDbContext _context;
    private readonly MockCourseLookup _courseLookup;
    private readonly EnrollmentService _service;

    public EnrollmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<EnrollmentDbContext>()
            .UseInMemoryDatabase(databaseName: $"EnrollmentTests_{Guid.NewGuid()}")
            .Options;

        _context = new EnrollmentDbContext(options);
        _courseLookup = new MockCourseLookup();
        _service = new EnrollmentService(_context, _courseLookup);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ── GetEnrollmentCountsByCourseAsync ──

    [Fact]
    public async Task GetEnrollmentCountsByCourseAsync_ReturnsCountsForCoursesWithEnrollments()
    {
        // Arrange
        var student1 = CreateStudent();
        var student2 = CreateStudent();
        var course1 = Guid.NewGuid();
        var course2 = Guid.NewGuid();
        _courseLookup.AddCourse(course1, "Course 1");
        _courseLookup.AddCourse(course2, "Course 2");

        await _service.EnrollAsync(student1.Id, course1);
        await _service.EnrollAsync(student2.Id, course1);
        await _service.EnrollAsync(student1.Id, course2);

        // Act
        var counts = await _service.GetEnrollmentCountsByCourseAsync(new[] { course1, course2 });

        // Assert
        Assert.Equal(2, counts[course1]);
        Assert.Equal(1, counts[course2]);
    }

    [Fact]
    public async Task GetEnrollmentCountsByCourseAsync_ExcludesCoursesWithZeroEnrollments()
    {
        // Arrange
        var student1 = CreateStudent();
        var course1 = Guid.NewGuid();
        var course2 = Guid.NewGuid(); // No enrollments
        _courseLookup.AddCourse(course1, "Course 1");
        _courseLookup.AddCourse(course2, "Course 2");

        await _service.EnrollAsync(student1.Id, course1);

        // Act
        var counts = await _service.GetEnrollmentCountsByCourseAsync(new[] { course1, course2 });

        // Assert
        Assert.Equal(1, counts[course1]);
        Assert.False(counts.ContainsKey(course2));
    }

    [Fact]
    public async Task GetEnrollmentCountsByCourseAsync_ReturnsEmptyForEmptyInput()
    {
        // Act
        var counts = await _service.GetEnrollmentCountsByCourseAsync(Array.Empty<Guid>());

        // Assert
        Assert.Empty(counts);
    }

    // ── GetPreferencesAsync ──

    [Fact]
    public async Task GetPreferencesAsync_ReturnsDefaultsForNonExistentStudent()
    {
        // Act
        var prefs = await _service.GetPreferencesAsync(Guid.NewGuid());

        // Assert
        Assert.True(prefs.EmailNotificationsEnabled);
        Assert.Equal("System", prefs.ThemePreference);
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsStoredValues()
    {
        // Arrange
        var student = CreateStudent();
        student.EmailNotificationsEnabled = false;
        student.ThemePreference = "Dark";
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        // Act
        var prefs = await _service.GetPreferencesAsync(student.Id);

        // Assert
        Assert.False(prefs.EmailNotificationsEnabled);
        Assert.Equal("Dark", prefs.ThemePreference);
    }

    // ── UpdatePreferencesAsync ──

    [Fact]
    public async Task UpdatePreferencesAsync_PersistsNewValues()
    {
        // Arrange
        var student = CreateStudent();
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdatePreferencesAsync(student.Id, false, "Light");

        // Assert
        var updated = await _context.Students.FindAsync(student.Id);
        Assert.NotNull(updated);
        Assert.False(updated.EmailNotificationsEnabled);
        Assert.Equal("Light", updated.ThemePreference);
    }

    [Fact]
    public async Task UpdatePreferencesAsync_NoOpForNonExistentStudent()
    {
        // Act — should not throw
        await _service.UpdatePreferencesAsync(Guid.NewGuid(), true, "System");

        // Assert — no students in DB
        Assert.Empty(await _context.Students.ToListAsync());
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
}

/// <summary>Minimal mock of ICourseLookup for unit tests.</summary>
public class MockCourseLookup : ICourseLookup
{
    private readonly Dictionary<Guid, CourseSummary> _courses = new();

    public void AddCourse(Guid id, string title) => _courses[id] = new CourseSummary(id, title, "General", Guid.NewGuid());

    public Task<CourseSummary?> GetCourseAsync(Guid id)
    {
        if (_courses.TryGetValue(id, out var course))
            return Task.FromResult<CourseSummary?>(course);
        return Task.FromResult<CourseSummary?>(null);
    }

    public Task<int> CountAsync() => Task.FromResult(_courses.Count);

    public Task<int> CountByOrgAsync(Guid organizationId) => Task.FromResult(_courses.Values.Count(c => c.OrganizationId == organizationId));

    public Task<IReadOnlyDictionary<Guid, int>> GetCourseCountsByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var orgs = organizationIds.ToHashSet();
        var dict = _courses.Values
            .Where(c => orgs.Contains(c.OrganizationId))
            .GroupBy(c => c.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(dict);
    }

    public Task<IList<string>> GetDistinctCategoriesAsync() =>
        Task.FromResult<IList<string>>(_courses.Values.Select(c => c.Category).Distinct().OrderBy(c => c).ToList());

    public Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds)
    {
        var ids = courseIds.ToHashSet();
        return Task.FromResult<IList<CourseSummary>>(_courses.Values.Where(c => ids.Contains(c.Id)).ToList());
    }

    public Task<IList<CourseSummary>> ListByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var orgs = organizationIds.ToHashSet();
        return Task.FromResult<IList<CourseSummary>>(_courses.Values.Where(c => orgs.Contains(c.OrganizationId)).ToList());
    }

    public Task<IList<CourseSummary>> ListAllAsync() => Task.FromResult<IList<CourseSummary>>(_courses.Values.ToList());
}
