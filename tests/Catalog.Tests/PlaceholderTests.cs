namespace Catalog.Tests;

/// <summary>
/// Smoke tests to verify the Catalog module assembly loads with all expected types.
/// Real unit/integration tests land with feature slices.
/// </summary>
public class PlaceholderTests
{
    [Fact]
    public void Module_assembly_loads() =>
        Assert.NotNull(typeof(LearningLms.Modules.Catalog.ModuleMarker));

    [Fact]
    public void Course_entity_exists() =>
        Assert.NotNull(typeof(LearningLms.Modules.Catalog.Domain.Course));

    [Fact]
    public void CourseCatalogService_exists() =>
        Assert.NotNull(typeof(LearningLms.Modules.Catalog.Application.CourseCatalogService));

    [Fact]
    public void CourseLookup_exists() =>
        Assert.NotNull(typeof(LearningLms.Modules.Catalog.Application.CourseLookup));

    [Fact]
    public void CatalogDbContext_exists() =>
        Assert.NotNull(typeof(LearningLms.Modules.Catalog.Infrastructure.CatalogDbContext));

    [Fact]
    public void CourseDto_exists() =>
        Assert.NotNull(typeof(LearningLms.Modules.Catalog.Endpoints.CourseDto));

    [Fact]
    public void ICourseLookup_contract_exists() =>
        Assert.NotNull(typeof(LearningLms.Contracts.Catalog.ICourseLookup));
}
