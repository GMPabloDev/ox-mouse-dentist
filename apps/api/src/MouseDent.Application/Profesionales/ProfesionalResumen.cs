using MouseDent.Domain.Auth;

namespace MouseDent.Application.Profesionales;

/// <summary>
/// Ficha del profesional con los datos de su cuenta y el estado de sus
/// asignaciones. Es el modelo de lectura de la lista: el nombre, el correo y el
/// teléfono se leen de la cuenta, y las especialidades y los locales se muestran
/// con el estado que hoy tienen en el catálogo y en el local.
/// </summary>
public sealed record ProfesionalResumen(
    Guid Id,
    Guid StaffAccountId,
    string DisplayName,
    string Email,
    string? Phone,
    string? Colegiatura,
    bool IsActive,
    EstadoDeCuenta AccountStatus,
    bool InvitationFailed,
    IReadOnlyList<ProfesionalResumenEspecialidad> Especialidades,
    IReadOnlyList<ProfesionalResumenLocal> Locales);

/// <summary>Especialidad asignada con su estado actual en el catálogo.</summary>
public sealed record ProfesionalResumenEspecialidad(Guid Id, string Name, bool IsActive);

/// <summary>Local de atención asignado con su estado actual.</summary>
public sealed record ProfesionalResumenLocal(Guid Id, string Name, bool IsActive);

/// <summary>
/// Filtros del listado. Todos son opcionales y se combinan. El estado de la ficha y
/// el estado de la cuenta son independientes, igual que en la ficha.
/// </summary>
public sealed record ProfesionalFiltro
{
    /// <summary>Nombre o correo. Nulo o vacío devuelve todas las fichas.</summary>
    public string? Search { get; init; }

    public Guid? EspecialidadId { get; init; }

    public Guid? LocalId { get; init; }

    /// <summary>Nulo devuelve ambas; verdadero solo fichas activas; falso solo inactivas.</summary>
    public bool? FichaActiva { get; init; }

    public EstadoDeCuenta? AccountStatus { get; init; }
}

/// <summary>
/// Consulta de la lista de profesionales. La implementación vive en Infrastructure
/// y une las fichas con las cuentas, las especialidades y los locales.
/// </summary>
public interface IListadoDeProfesionales
{
    /// <summary>Fichas que cumplen el filtro, ordenadas por nombre y correo de la cuenta.</summary>
    Task<IReadOnlyList<ProfesionalResumen>> ListAsync(
        ProfesionalFiltro filtro,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ficha con ese identificador, con la misma forma que la lista, o <c>null</c>
    /// cuando no existe. Una cuenta sin ficha nunca aparece.
    /// </summary>
    Task<ProfesionalResumen?> BuscarAsync(Guid id, CancellationToken cancellationToken);
}
