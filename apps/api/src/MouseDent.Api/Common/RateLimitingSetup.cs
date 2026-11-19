using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace MouseDent.Api.Common;

public static class RateLimitPolicies
{
    public const string Login = "login";

    /// <summary>Solicitudes públicas de recuperación de acceso.</summary>
    public const string PasswordRecovery = "password-recovery";
}

/// <summary>
/// Límites por dirección de origen. Viven en la configuración para poder
/// ajustarlos y probar el límite sin recompilar.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    public int LoginPermitLimit { get; init; } = 10;

    public int PasswordRecoveryPermitLimit { get; init; } = 5;

    public int WindowSeconds { get; init; } = 60;
}

public static class RateLimitingSetup
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Los límites se leen al atender cada solicitud, no al registrar servicios:
        // así la configuración final del host decide los valores.
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(
                RateLimitPolicies.Login,
                context => Partition(context, limits => limits.LoginPermitLimit));

            options.AddPolicy(
                RateLimitPolicies.PasswordRecovery,
                context => Partition(context, limits => limits.PasswordRecoveryPermitLimit));
        });

        return services;
    }

    private static RateLimitPartition<string> Partition(
        HttpContext context,
        Func<RateLimitOptions, int> permitLimit)
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit(limits),
                Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                QueueLimit = 0,
            });
    }
}
