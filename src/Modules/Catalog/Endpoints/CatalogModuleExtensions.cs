using Microsoft.Extensions.DependencyInjection;
using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Catalog.Application;

namespace LibreLms.Modules.Catalog;

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
