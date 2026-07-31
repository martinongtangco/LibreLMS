using Microsoft.Extensions.DependencyInjection;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Modules.Management;

/// <summary>Registration extension for the Management module's DI services.</summary>
public static class ManagementModuleExtensions
{
    public static IServiceCollection AddManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationLookup, OrganizationLookup>();
        services.AddScoped<IUserInfoLookup, UserInfoLookup>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<UserService>();
        services.AddScoped<CourseVisibilityService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AdminEnrollmentService>();
        return services;
    }
}
