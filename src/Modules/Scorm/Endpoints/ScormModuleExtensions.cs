using Microsoft.Extensions.DependencyInjection;
using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Infrastructure;

namespace LibreLms.Modules.Scorm.Endpoints;

/// <summary>Registration extension for the Scorm module's DI services.</summary>
public static class ScormModuleExtensions
{
    public static IServiceCollection AddScormModule(this IServiceCollection services)
    {
        services.AddScoped<ScormSessionService>();
        services.AddScoped<ScormAttemptService>();
        services.AddScoped<IScormSessionStore, ScormSessionStore>();
        services.AddScoped<ManifestParser>();
        return services;
    }

    /// <summary>
    /// Bind the wwwRootPath to the ScormPackageService constructor parameter.
    /// Call this after AddScormModule() and pass the WebRootPath from Host.
    /// </summary>
    public static IServiceCollection ConfigureScormModule(this IServiceCollection services, string wwwRootPath)
    {
        // Replace the default ScormPackageService registration with one that captures wwwRootPath
        services.AddScoped<ScormPackageService>(sp =>
            new ScormPackageService(
                sp.GetRequiredService<ScormDbContext>(),
                sp.GetRequiredService<ManifestParser>(),
                wwwRootPath));
        return services;
    }
}
