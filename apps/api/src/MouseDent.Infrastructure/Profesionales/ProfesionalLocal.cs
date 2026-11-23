namespace MouseDent.Infrastructure.Profesionales;

/// <summary>
/// Local de atención asignado a una ficha. Se conserva cuando el local se
/// desactiva: el local inactivo simplemente no autoriza operaciones ni se ofrece
/// para añadirlo a otra ficha.
/// </summary>
public sealed class ProfesionalLocal
{
    public Guid ProfesionalId { get; set; }

    public ProfesionalGuardado Profesional { get; set; } = null!;

    public Guid LocalId { get; set; }
}
