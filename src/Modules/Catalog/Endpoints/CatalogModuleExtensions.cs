using Microsoft.Extensions.DependencyInjection;
using LearningLms.Contracts.Catalog;
using LearningLms.Modules.Catalog.Application;

namespace LearningLms.Modules.Catalog;

/// <summary>Registration extension for the Catalog module's DI services.</summary>
public static class CatalogModuleExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<ICourseLookup, CourseLookup>();
        services.AddScoped<CourseCatalogService>();
        return services;
    }
}
