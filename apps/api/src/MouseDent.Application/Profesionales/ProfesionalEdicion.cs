namespace MouseDent.Application.Profesionales;

/// <summary>
/// Datos editables de una ficha. El nombre, el correo, el teléfono y el perfil
/// viven en la cuenta y se editan en el módulo de personal: la ficha solo cambia
/// su número de colegiatura y sus asignaciones.
/// </summary>
public sealed record ProfesionalEdicionCommand(
    string? Colegiatura,
    IReadOnlyList<Guid> EspecialidadIds,
    IReadOnlyList<Guid> LocalIds);

public enum ProfesionalEdicionStatus
{
    /// <summary>La ficha y sus asignaciones quedaron guardadas.</summary>
    Saved,

    /// <summary>No existe una ficha con ese identificador.</summary>
    NotFound,

    /// <summary>El número de colegiatura supera el límite admitido.</summary>
    InvalidColegiatura,

    /// <summary>Otra ficha, incluso inactiva, tiene el mismo número de colegiatura.</summary>
    DuplicateColegiatura,

    /// <summary>Alguna especialidad nueva no existe o está inactiva.</summary>
    EspecialidadInactiva,

    /// <summary>Algún local nuevo no existe o está inactivo.</summary>
    LocalInactivo,

    /// <summary>La ficha quedaría activa sin una especialidad activa o sin un local activo.</summary>
    ActivacionIncompleta,
}

/// <summary>Resultado de la edición. Solo <see cref="ProfesionalEdicionStatus.Saved"/> escribe.</summary>
public sealed record ProfesionalEdicionOutcome
{
    private ProfesionalEdicionOutcome(ProfesionalEdicionStatus status) => Status = status;

    public ProfesionalEdicionStatus Status { get; }

    public static ProfesionalEdicionOutcome Saved() => new(ProfesionalEdicionStatus.Saved);

    public static ProfesionalEdicionOutcome Rejected(ProfesionalEdicionStatus status) => new(status);
}
