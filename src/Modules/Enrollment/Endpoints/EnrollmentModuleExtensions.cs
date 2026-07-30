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
        return services;
    }
}
