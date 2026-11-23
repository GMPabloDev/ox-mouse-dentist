using Microsoft.Extensions.DependencyInjection;
using MouseDent.Application.Auth;
using MouseDent.Application.Catalogo;
using MouseDent.Application.Locales;

namespace MouseDent.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<CatalogoService>();
        services.AddScoped<StaffAccessService>();
        services.AddScoped<StaffManagementService>();
        services.AddScoped<LocalAccessService>();
        services.AddScoped<LocalManagementService>();

        return services;
    }
}
