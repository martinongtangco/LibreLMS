namespace Enrollment.Tests;

/// <summary>
/// Smoke tests to verify the Enrollment module assembly loads with all expected types.
/// Real unit/integration tests land with feature slices.
/// </summary>
public class PlaceholderTests
{
    [Fact]
    public void Module_assembly_loads() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.ModuleMarker));

    [Fact]
    public void Student_entity_exists() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.Domain.Student));

    [Fact]
    public void Enrollment_entity_exists() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.Domain.Enrollment));

    [Fact]
    public void EnrollmentService_exists() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.Application.EnrollmentService));

    [Fact]
    public void EnrollmentDbContext_exists() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.Infrastructure.EnrollmentDbContext));

    [Fact]
    public void EnrollRequest_dto_exists() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.Endpoints.EnrollRequest));

    [Fact]
    public void EnrollmentDto_exists() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.Endpoints.EnrollmentDto));

    [Fact]
    public void MyEnrollmentDto_exists() =>
        Assert.NotNull(typeof(LibreLms.Modules.Enrollment.Endpoints.MyEnrollmentDto));
}
