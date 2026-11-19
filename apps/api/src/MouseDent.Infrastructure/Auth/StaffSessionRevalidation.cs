using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using MouseDent.Application.Auth;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Comprueba en cada solicitud autenticada que la cuenta del token siga vigente:
/// activa, con contraseña establecida y con el mismo correo y perfil con los que
/// se emitió. Así una desactivación, un cambio de perfil o un cambio de correo
/// se aplican en la siguiente solicitud sin esperar al vencimiento del token.
/// </summary>
internal static class StaffSessionRevalidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var subject = principal?.FindFirstValue(AuthClaims.Subject);
        var email = principal?.FindFirstValue(AuthClaims.Email);
        var profile = principal?.FindAll(AuthClaims.Role).Select(claim => claim.Value).ToArray() ?? [];
        var sessionVersion = principal?.FindFirstValue(AuthClaims.SessionVersion);

        // Sin identificador, correo, versión de sesión y un único perfil no hay sesión que validar.
        if (!Guid.TryParse(subject, out var accountId)
            || !int.TryParse(sessionVersion, out var version)
            || string.IsNullOrWhiteSpace(email)
            || profile.Length != 1)
        {
            context.Fail("El token no identifica una cuenta con un perfil.");
            return;
        }

        var auth = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
        var account = await auth.ValidateSessionAsync(
            accountId,
            email,
            profile[0],
            version,
            context.HttpContext.RequestAborted);

        if (account is null)
        {
            context.Fail("La sesión ya no está vigente.");
        }
    }
}
