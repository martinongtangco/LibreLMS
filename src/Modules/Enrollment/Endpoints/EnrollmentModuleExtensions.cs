using Microsoft.Extensions.DependencyInjection;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Application;

namespace LibreLms.Modules.Enrollment;

/// <summary>Registration extension for the Enrollment module's DI services.</summary>
public static class EnrollmentModuleExtensions
{
    public static IServiceCollection AddEnrollmentModule(this IServiceCollection services)
    {
        services.AddScoped<EnrollmentService>();
        services.AddScoped<IEnrollmentLookup, EnrollmentLookup>();

        // Spec 027: shared credential core (stateless — singleton) and cross-module
        // account/enrollment contracts (scoped, over EnrollmentDbContext).
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<CredentialPolicy>();
        services.AddScoped<IUserProvisioning, UserProvisioningService>();
        services.AddScoped<IUserLookup, UserLookupService>();
        services.AddScoped<IEnrollmentAdmin, EnrollmentAdminService>();
        return services;
    }
}
