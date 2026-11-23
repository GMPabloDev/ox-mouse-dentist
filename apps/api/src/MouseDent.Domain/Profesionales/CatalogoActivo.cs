namespace MouseDent.Domain.Profesionales;

/// <summary>
/// Identificadores que hoy están activos en el catálogo de especialidades y en
/// los locales de atención. La ficha solo conoce los identificadores asignados y
/// no duplica su estado, así que el llamador resuelve este conjunto antes de
/// crear, editar o activar; el dominio comprueba sus reglas sin depender de la
/// persistencia ni de otras entidades.
/// </summary>
public sealed class CatalogoActivo
{
    private readonly HashSet<Guid> _especialidades;
    private readonly HashSet<Guid> _locales;

    public CatalogoActivo(IEnumerable<Guid> especialidades, IEnumerable<Guid> locales)
    {
        ArgumentNullException.ThrowIfNull(especialidades);
        ArgumentNullException.ThrowIfNull(locales);

        _especialidades = [.. especialidades];
        _locales = [.. locales];
    }

    public bool EsEspecialidadActiva(Guid id) => _especialidades.Contains(id);

    public bool EsLocalActivo(Guid id) => _locales.Contains(id);
}
