using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

public enum StaffOutcomeStatus
{
    Saved,

    NotFound,

    /// <summary>La cuenta cambió mientras se preparaba la edición.</summary>
    ConcurrentChange,

    /// <summary>Ya existe una cuenta con ese correo, aunque esté inactiva o pendiente.</summary>
    DuplicateEmail,

    /// <summary>La operación dejaría sin administrador activo con contraseña.</summary>
    LastAdministrator,

    /// <summary>Algún local solicitado no existe o está inactivo.</summary>
    LocalsUnavailable,

    /// <summary>Una cuenta nueva de recepción necesita al menos un local.</summary>
    ReceptionRequiresLocal,

    /// <summary>Solo las cuentas de recepción reciben locales.</summary>
    LocalsNotAllowed,

    /// <summary>La cuenta está desactivada y no admite enlaces utilizables.</summary>
    AccountInactive,

    /// <summary>La cuenta ya estableció su contraseña y no necesita invitación.</summary>
    PasswordAlreadyEstablished,

    /// <summary>La cuenta se guardó, pero el correo con el enlace no llegó.</summary>
    InvitationFailed,
}

/// <summary>Resultado de una operación de gestión de cuentas del personal.</summary>
public sealed record StaffOutcome
{
    private StaffOutcome(StaffAccount? account, StaffOutcomeStatus status)
    {
        Account = account;
        Status = status;
    }

    public StaffAccount? Account { get; }

    public StaffOutcomeStatus Status { get; }

    public static StaffOutcome Saved(StaffAccount account) => new(account, StaffOutcomeStatus.Saved);

    public static StaffOutcome Failed(StaffOutcomeStatus status) => new(null, status);

    /// <summary>Cuenta guardada cuyo correo de invitación no se pudo entregar.</summary>
    public static StaffOutcome InvitationFailed(StaffAccount account) =>
        new(account, StaffOutcomeStatus.InvitationFailed);
}
