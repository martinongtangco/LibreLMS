using Microsoft.Extensions.DependencyInjection;
using LearningLms.Modules.Enrollment.Application;

namespace LearningLms.Modules.Enrollment;

/// <summary>Registration extension for the Enrollment module's DI services.</summary>
public static class EnrollmentModuleExtensions
{
    public static IServiceCollection AddEnrollmentModule(this IServiceCollection services)
    {
        services.AddScoped<EnrollmentService>();
        return services;
    }
}
