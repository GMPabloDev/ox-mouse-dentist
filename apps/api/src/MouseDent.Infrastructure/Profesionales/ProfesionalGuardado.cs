namespace MouseDent.Infrastructure.Profesionales;

/// <summary>
/// Fila de la ficha del profesional en el almacén. No sale de Infrastructure: los
/// casos de uso usan <see cref="MouseDent.Domain.Profesionales.Profesional"/>. Las
/// especialidades y los locales son relaciones y guardan solo el identificador
/// asignado; su estado se lee del catálogo y del local, así que desactivarlos
/// conserva la asignación sin duplicar el estado en la ficha.
/// </summary>
public sealed class ProfesionalGuardado
{
    public Guid Id { get; set; }

    /// <summary>Cuenta vinculada. Obligatoria y única, con índice único en la base.</summary>
    public Guid StaffAccountId { get; set; }

    public string? Colegiatura { get; set; }

    public string? ColegiaturaNormalizada { get; set; }

    public bool IsActive { get; set; }

    public ICollection<ProfesionalEspecialidad> Especialidades { get; set; } = [];

    public ICollection<ProfesionalLocal> Locales { get; set; } = [];
}
