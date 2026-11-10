using Microsoft.Extensions.DependencyInjection;
using MouseDent.Application.Auth;

namespace MouseDent.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();

        return services;
    }
}
