using MouseDent.Domain.Auth;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Traduce una cuenta nueva de dominio a su fila de Identity. La escritura de
/// cuentas y el alta de profesionales comparten este mapeo para que una cuenta
/// creada desde cualquiera de los dos caminos guarde los mismos datos.
/// </summary>
internal static class StaffUserFactory
{
    public static StaffUser Nuevo(StaffAccount account) => new()
    {
        Id = account.Id,

        // El correo identifica la cuenta y se valida en el borde. El nombre de
        // usuario solo es el identificador interno de Identity: usar el correo
        // rechazaría direcciones válidas con tildes o eñes.
        UserName = account.Id.ToString(),
        Email = account.Email,
        EmailConfirmed = true,
        DisplayName = account.DisplayName,
        PhoneNumber = account.Phone,
        Profile = account.Profile,
        IsActive = account.IsActive,
        PasswordEstablishedAt = account.PasswordEstablishedAt,
        SessionVersion = account.SessionVersion,
    };
}
