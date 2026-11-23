namespace MouseDent.Application.Profesionales;

/// <summary>
/// Resultado de activar o desactivar una ficha. Desactivar conserva las
/// asignaciones y la cuenta; reactivar exige al menos una especialidad activa y
/// un local activo.
/// </summary>
public enum ProfesionalEstadoStatus
{
    /// <summary>El estado de la ficha quedó guardado.</summary>
    Saved,

    /// <summary>No existe una ficha con ese identificador.</summary>
    NotFound,

    /// <summary>La ficha no recuperó una especialidad activa o un local activo.</summary>
    ActivacionIncompleta,
}

public sealed record ProfesionalEstadoOutcome
{
    private ProfesionalEstadoOutcome(ProfesionalEstadoStatus status) => Status = status;

    public ProfesionalEstadoStatus Status { get; }

    public static ProfesionalEstadoOutcome Saved() => new(ProfesionalEstadoStatus.Saved);

    public static ProfesionalEstadoOutcome Rejected(ProfesionalEstadoStatus status) => new(status);
}
