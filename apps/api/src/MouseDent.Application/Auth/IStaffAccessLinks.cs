using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

/// <summary>
/// Puerto del almacén de enlaces de acceso. La implementación vive en
/// Infrastructure y consume el enlace y guarda la contraseña en una sola
/// transacción.
/// </summary>
public interface IStaffAccessLinks
{
    /// <summary>
    /// Inserta el enlace e invalida, en la misma transacción, los enlaces
    /// anteriores de la cuenta. Los enlaces anteriores dejan de servir aunque el
    /// envío del correo nuevo falle. Solo inserta si la cuenta sigue activa, con
    /// el mismo correo y versión de sesión de <paramref name="expected"/> y,
    /// cuando <paramref name="requirePending"/> es verdadero, sigue pendiente.
    /// Devuelve falso sin modificar enlaces si la cuenta ya no cumple estas condiciones.
    /// </summary>
    Task<bool> ReplacePendingAsync(StaffAccessLink link, StaffAccount expected, bool requirePending, CancellationToken cancellationToken);

    /// <summary>
    /// Consume el enlace con el hash indicado, establece la contraseña con la
    /// política configurada e invalida las sesiones anteriores. Si el enlace no
    /// sirve, no cambia nada.
    /// </summary>
    Task<PasswordResetResult> ResetPasswordAsync(
        string tokenHash,
        string password,
        CancellationToken cancellationToken);
}

public enum PasswordResetStatus
{
    /// <summary>La contraseña quedó establecida y las sesiones anteriores dejan de servir.</summary>
    Reset,

    /// <summary>El enlace no existe, ya se usó, venció o la cuenta no está activa.</summary>
    LinkUnusable,

    /// <summary>La política de contraseña rechazó el valor.</summary>
    PasswordRejected,
}

/// <summary>
/// Resultado de consumir un enlace. Los errores de contraseña llegan como
/// códigos de la política, que el borde traduce al mensaje del formulario.
/// </summary>
public sealed record PasswordResetResult(PasswordResetStatus Status, IReadOnlyList<string> PasswordErrors)
{
    public static PasswordResetResult Reset() => new(PasswordResetStatus.Reset, []);

    public static PasswordResetResult LinkUnusable() => new(PasswordResetStatus.LinkUnusable, []);

    public static PasswordResetResult PasswordRejected(IReadOnlyList<string> errors) =>
        new(PasswordResetStatus.PasswordRejected, errors);
}
