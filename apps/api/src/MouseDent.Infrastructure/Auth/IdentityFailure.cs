using Microsoft.AspNetCore.Identity;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Traduce los fallos de Identity de una cuenta. El correo tiene índice único y su
/// duplicado llega como <c>DuplicateEmail</c>; cualquier otro fallo, incluido un
/// <c>DuplicateUserName</c> que no sea el correo, es un error de programación que no
/// debe pasar como resultado del caso de uso.
/// </summary>
internal static class IdentityFailure
{
    public static bool IsDuplicateEmail(IdentityResult result) =>
        result.Errors.Any(error => error.Code is "DuplicateEmail");

    public static InvalidOperationException Unexpected(IdentityResult result) =>
        new($"No se pudo guardar la cuenta: {string.Join("; ", result.Errors.Select(error => error.Description))}");
}
