using MouseDent.Domain.Auth;

namespace MouseDent.Application.Profesionales;

public enum ProfesionalInvitationStatus
{
    /// <summary>El correo con el enlace se entregó; la marca de fallo queda limpia.</summary>
    Sent,

    /// <summary>No existe una ficha con ese identificador.</summary>
    ProfessionalNotFound,

    /// <summary>La cuenta está desactivada y no admite enlaces utilizables.</summary>
    AccountInactive,

    /// <summary>La cuenta ya estableció su contraseña y no necesita invitación.</summary>
    PasswordAlreadyEstablished,

    /// <summary>El enlace se emitió, pero el correo no se pudo entregar.</summary>
    DeliveryFailed,
}

/// <summary>
/// Resultado del reenvío de la invitación pendiente de una ficha. La cuenta solo
/// está presente cuando el enlace se emitió.
/// </summary>
public sealed record ProfesionalInvitationOutcome
{
    private ProfesionalInvitationOutcome(ProfesionalInvitationStatus status, StaffAccount? account)
    {
        Status = status;
        Account = account;
    }

    public ProfesionalInvitationStatus Status { get; }

    public StaffAccount? Account { get; }

    public static ProfesionalInvitationOutcome Sent(StaffAccount account) =>
        new(ProfesionalInvitationStatus.Sent, account);

    public static ProfesionalInvitationOutcome DeliveryFailed(StaffAccount account) =>
        new(ProfesionalInvitationStatus.DeliveryFailed, account);

    public static ProfesionalInvitationOutcome Rejected(ProfesionalInvitationStatus status) =>
        new(status, null);
}
