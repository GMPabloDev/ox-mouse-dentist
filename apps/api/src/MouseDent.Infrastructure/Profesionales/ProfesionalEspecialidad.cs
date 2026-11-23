namespace MouseDent.Infrastructure.Profesionales;

/// <summary>
/// Asignación de una especialidad del catálogo a una ficha. Se conserva cuando la
/// especialidad se desactiva: la ficha la muestra como inactiva leyendo el estado
/// del catálogo.
/// </summary>
public sealed class ProfesionalEspecialidad
{
    public Guid ProfesionalId { get; set; }

    public ProfesionalGuardado Profesional { get; set; } = null!;

    public Guid EspecialidadId { get; set; }
}
