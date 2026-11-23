using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

/// <summary>
/// Puerto hacia el almacén de cuentas del personal. La implementación vive en
/// Infrastructure y el caso de uso no la conoce.
/// </summary>
public interface IStaffAccounts
{
    /// <summary>
    /// Devuelve la cuenta solo si las credenciales son correctas, la cuenta está
    /// activa y ya estableció su contraseña. Una cuenta pendiente o inactiva no
    /// inicia sesión.
    /// </summary>
    Task<StaffAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Cuenta con ese correo, sea cual sea su estado, o <c>null</c> si no existe.
    /// La recuperación pública la usa sin revelar el resultado.
    /// </summary>
    Task<StaffAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Cuentas del personal, incluidas las pendientes y las inactivas, filtradas
    /// por nombre o correo. <paramref name="search"/> nulo o vacío devuelve todas.
    /// </summary>
    Task<IReadOnlyList<StaffAccount>> ListAsync(string? search, CancellationToken cancellationToken);

    /// <summary>Inserta la cuenta pendiente. El correo debe ser único.</summary>
    Task<StaffSaveResult> CreateAsync(StaffAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda los cambios de una cuenta existente y aplica la protección del
    /// último administrador disponible. La comprobación y la escritura ocurren
    /// en una sola transacción, de modo que dos solicitudes concurrentes no
    /// puedan dejar el sistema sin administración. Si el estado no coincide con
    /// <paramref name="expected"/>, no escribe y devuelve <see cref="StaffSaveResult.Changed"/>.
    /// Cambiar el correo o desactivar también invalida los enlaces pendientes en
    /// esa misma transacción.
    /// </summary>
    Task<StaffSaveResult> UpdateAsync(StaffAccount account, StaffAccount expected, CancellationToken cancellationToken);

    /// <summary>
    /// Marca el fallo del último envío de invitación de la cuenta, que la lista de
    /// profesionales muestra mientras la cuenta siga pendiente. Es una actualización
    /// acotada: no cambia la versión de la sesión y no pasa por la protección del
    /// último administrador.
    /// </summary>
    Task RecordInvitationFailureAsync(
        Guid accountId,
        DateTime failedAt,
        CancellationToken cancellationToken);

    /// <summary>Limpia la marca de fallo porque una invitación se entregó.</summary>
    Task ClearInvitationFailureAsync(Guid accountId, CancellationToken cancellationToken);
}

public enum StaffSaveResult
{
    Saved,

    NotFound,

    /// <summary>La cuenta cambió después de leerla; se debe volver a aplicar la operación.</summary>
    Changed,

    /// <summary>El correo ya pertenece a otra cuenta, incluida una inactiva o pendiente.</summary>
    DuplicateEmail,

    /// <summary>La operación dejaría el sistema sin un administrador activo con contraseña.</summary>
    LastAdministrator,

    LocalsUnavailable,
}
