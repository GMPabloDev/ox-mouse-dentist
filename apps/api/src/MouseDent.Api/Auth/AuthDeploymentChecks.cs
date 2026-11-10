using MouseDent.Application.Auth;

namespace MouseDent.Api.Auth;

public static class AuthDeploymentChecks
{
    private const string DevelopmentKeyPrefix = "dev-only";

    /// <summary>
    /// Falla al arrancar si se despliega con la clave de desarrollo. Se invoca
    /// después de <c>builder.Build()</c> para no interferir con la generación de
    /// OpenAPI en build.
    /// </summary>
    public static void EnsureDeployableSigningKey(this WebApplication app)
    {
        if (app.Environment.IsDevelopment()
            || app.Environment.IsEnvironment(ApiEnvironments.OpenApiBuild))
        {
            return;
        }

        var jwt = app.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.StartsWith(DevelopmentKeyPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey conserva el valor de desarrollo. Configura una clave propia con Jwt__SigningKey antes de desplegar.");
        }
    }
}
