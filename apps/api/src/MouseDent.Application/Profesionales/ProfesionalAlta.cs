using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Profesionales;

/// <summary>
/// Datos del alta de una ficha. El teléfono no forma parte del alta: la cuenta se
/// crea sin teléfono y se edita en el módulo de personal. El nombre solo se usa
/// cuando la cuenta se crea; si el correo ya pertenece a una cuenta, el nombre
/// vive en la cuenta y el del alta se ignora.
/// </summary>
public sealed record ProfesionalAltaCommand(
    string DisplayName,
    string Email,
    string? Colegiatura,
    IReadOnlyList<Guid> EspecialidadIds,
    IReadOnlyList<Guid> LocalIds);

public enum ProfesionalAltaStatus
{
    /// <summary>Ficha y cuenta guardadas, con la invitación entregada.</summary>
    Saved,

    /// <summary>Ficha y cuenta guardadas, pero el correo con la invitación no salió.</summary>
    InvitationFailed,

    /// <summary>El correo ya pertenece a una cuenta con otro perfil.</summary>
    WrongProfile,

    /// <summary>El correo ya pertenece a una cuenta que tiene ficha.</summary>
    AccountAlreadyLinked,

    /// <summary>Otra alta simultánea tomó el correo entre la lectura y la escritura.</summary>
    DuplicateEmail,

    /// <summary>La cuenta a vincular desapareció entre la lectura y la escritura.</summary>
    AccountNotFound,

    /// <summary>El número de colegiatura supera el límite admitido.</summary>
    InvalidColegiatura,

    /// <summary>Otra ficha, incluso inactiva, tiene el mismo número de colegiatura.</summary>
    DuplicateColegiatura,

    /// <summary>Alguna especialidad no existe o está inactiva.</summary>
    EspecialidadInactiva,

    /// <summary>Algún local no existe o está inactivo.</summary>
    LocalInactivo,

    /// <summary>La ficha quedaría activa sin una especialidad activa o sin un local activo.</summary>
    ActivacionIncompleta,
}

/// <summary>
/// Resultado del alta. <see cref="Ficha"/> solo está presente cuando la ficha quedó
/// guardada, incluso si la invitación falló; todo rechazo deja la base como estaba.
/// </summary>
public sealed record ProfesionalAltaOutcome
{
    private ProfesionalAltaOutcome(ProfesionalAltaStatus status, Profesional? ficha)
    {
        Status = status;
        Ficha = ficha;
    }

    public ProfesionalAltaStatus Status { get; }

    public Profesional? Ficha { get; }

    public static ProfesionalAltaOutcome Saved(Profesional ficha) =>
        new(ProfesionalAltaStatus.Saved, ficha);

    /// <summary>La ficha y la cuenta quedaron guardadas; el correo no se entregó.</summary>
    public static ProfesionalAltaOutcome InvitationFailed(Profesional ficha) =>
        new(ProfesionalAltaStatus.InvitationFailed, ficha);

    public static ProfesionalAltaOutcome Failed(ProfesionalAltaStatus status) => new(status, null);
}
