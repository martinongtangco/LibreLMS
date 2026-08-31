using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Enrollment.Tests;

/// <summary>
/// Spec 048 (E1) — unit tests for the bulk enrollment lookup
/// <c>EnrollmentLookup.GetEnrolledCourseIdsAsync</c>. Same EF InMemory pattern as
/// EnrollmentServiceTests: exercises the query shape (WHERE StudentId = @s AND
/// CourseId IN @ids). The page model's single call site (one call + HashSet
/// membership instead of a per-row loop) is guarded by the E2E browse/pagination
/// specs (02, 11, 16).
/// </summary>
public class EnrollmentLookupBulkTests : IDisposable
{
    private readonly EnrollmentDbContext _context;
    private readonly EnrollmentLookup _lookup;

    public EnrollmentLookupBulkTests()
    {
        var options = new DbContextOptionsBuilder<EnrollmentDbContext>()
            .UseInMemoryDatabase(databaseName: $"EnrollmentLookupTests_{Guid.NewGuid()}")
            .Options;

        _context = new EnrollmentDbContext(options);
        _lookup = new EnrollmentLookup(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetEnrolledCourseIdsAsync_ReturnsOnlyTheEnrolledSubset()
    {
        var student = CreateStudent();
        var enrolled1 = Guid.NewGuid();
        var enrolled2 = Guid.NewGuid();
        var notEnrolled = Guid.NewGuid();
        var alsoNotEnrolled = Guid.NewGuid();

        SeedEnrollment(student.Id, enrolled1);
        SeedEnrollment(student.Id, enrolled2);
        await _context.SaveChangesAsync();

        var result = await _lookup.GetEnrolledCourseIdsAsync(
            student.Id, new[] { enrolled1, enrolled2, notEnrolled, alsoNotEnrolled });

        var set = result.ToHashSet();
        Assert.Equal(2, set.Count);
        Assert.Contains(enrolled1, set);
        Assert.Contains(enrolled2, set);
        Assert.DoesNotContain(notEnrolled, set);
        Assert.DoesNotContain(alsoNotEnrolled, set);
    }

    [Fact]
    public async Task GetEnrolledCourseIdsAsync_DoesNotLeakAcrossStudents_AgreesWithIsEnrolledAsync()
    {
        var alice = CreateStudent();
        var bob = CreateStudent();
        var aliceCourse = Guid.NewGuid();

        SeedEnrollment(alice.Id, aliceCourse);
        await _context.SaveChangesAsync();

        var result = await _lookup.GetEnrolledCourseIdsAsync(bob.Id, new[] { aliceCourse });

        Assert.Empty(result);
        // The bulk and per-row methods must agree.
        Assert.True(await _lookup.IsEnrolledAsync(alice.Id, aliceCourse));
        Assert.False(await _lookup.IsEnrolledAsync(bob.Id, aliceCourse));
    }

    [Fact]
    public async Task GetEnrolledCourseIdsAsync_EmptyInput_ReturnsEmpty()
    {
        var result = await _lookup.GetEnrolledCourseIdsAsync(Guid.NewGuid(), Array.Empty<Guid>());

        Assert.Empty(result);
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
