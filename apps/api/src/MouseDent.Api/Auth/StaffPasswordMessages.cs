using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MouseDent.Api.Auth;

/// <summary>
/// Traduce los códigos de la política de contraseña de Identity a los mensajes
/// del formulario. La política vive en la configuración de Identity; este archivo
/// solo la explica en español.
/// </summary>
internal static class StaffPasswordMessages
{
    public static IReadOnlyList<string> ToMessages(IEnumerable<string> codes, IdentityOptions options)
    {
        var policy = options.Password;

        return
        [
            .. codes.Select(code => code switch
            {
                "PasswordTooShort" => $"La contraseña debe tener al menos {policy.RequiredLength} caracteres.",
                "PasswordRequiresDigit" => "La contraseña debe incluir al menos un número.",
                "PasswordRequiresLower" => "La contraseña debe incluir al menos una minúscula.",
                "PasswordRequiresUpper" => "La contraseña debe incluir al menos una mayúscula.",
                "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir al menos un símbolo.",
                "PasswordRequiresUniqueChars" =>
                    $"La contraseña debe usar al menos {policy.RequiredUniqueChars} caracteres distintos.",
                _ => "La contraseña no cumple la política configurada.",
            }),
        ];
    }
}
