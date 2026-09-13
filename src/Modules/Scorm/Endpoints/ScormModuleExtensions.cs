using Microsoft.Extensions.DependencyInjection;
using LibreLms.Contracts.Scorm;
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
    /// Spec 049: the upload caps default to <see cref="ScormPackageService.DefaultMaxEntryCount"/>
    /// and <see cref="ScormPackageService.DefaultMaxUncompressedBytes"/>; pass overrides here
    /// (e.g. from Host configuration) to change them.
    /// </summary>
    public static IServiceCollection ConfigureScormModule(
        this IServiceCollection services,
        string wwwRootPath,
        int maxEntryCount = ScormPackageService.DefaultMaxEntryCount,
        long maxUncompressedBytes = ScormPackageService.DefaultMaxUncompressedBytes)
    {
        // Register ScormPackageService with wwwRootPath, as both concrete type and IScormPackageService contract
        services.AddScoped<ScormPackageService>(sp =>
            new ScormPackageService(
                sp.GetRequiredService<ScormDbContext>(),
                sp.GetRequiredService<ManifestParser>(),
                wwwRootPath,
                maxEntryCount,
                maxUncompressedBytes));
        // Register the contract interface for cross-module access (Constitution Principle III)
        services.AddScoped<IScormPackageService>(sp =>
            sp.GetRequiredService<ScormPackageService>());
        return services;
    }
}
