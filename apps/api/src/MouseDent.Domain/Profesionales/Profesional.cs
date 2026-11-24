namespace MouseDent.Domain.Profesionales;

/// <summary>
/// Ficha del profesional. No guarda nombre, correo ni teléfono: esos datos viven
/// en la cuenta vinculada, que es obligatoria y única. La ficha aporta el número
/// de colegiatura, las especialidades, los locales de atención y su estado, que
/// es independiente del estado de la cuenta. Desactivarla conserva las
/// asignaciones y la cuenta; no existe borrado definitivo.
/// </summary>
public sealed class Profesional
{
    private readonly List<Guid> _especialidadIds = [];
    private readonly List<Guid> _localIds = [];

    private Profesional()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Cuenta vinculada. Obligatoria y única: una cuenta tiene como máximo una
    /// ficha, y la persistencia lo garantiza con un índice único.
    /// </summary>
    public Guid StaffAccountId { get; private set; }

    /// <summary>Número visible, con los espacios exteriores ya quitados.</summary>
    public string? Colegiatura { get; private set; }

    /// <summary>Clave de unicidad: sin espacios exteriores ni intermedios y en mayúsculas.</summary>
    public string? ColegiaturaNormalizada { get; private set; }

    /// <summary>Estado de la ficha, independiente del estado de la cuenta.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Especialidades asignadas, sin repetidos y ordenadas por identificador.</summary>
    public IReadOnlyList<Guid> EspecialidadIds => _especialidadIds;

    /// <summary>Locales de atención asignados, sin repetidos y ordenados por identificador.</summary>
    public IReadOnlyList<Guid> LocalIds => _localIds;

    /// <summary>
    /// Crea una ficha activa. Exige una cuenta, una especialidad activa y un local
    /// activo, y no admite asignar especialidades ni locales inactivos. Se valida con
    /// la misma regla que la edición de una ficha activa, partiendo de asignaciones
    /// actuales vacías.
    /// </summary>
    public static Profesional Crear(
        Guid staffAccountId,
        string? colegiatura,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds,
        CatalogoActivo activos)
    {
        if (staffAccountId == Guid.Empty)
        {
            throw new ArgumentException("La ficha necesita una cuenta.", nameof(staffAccountId));
        }

        ArgumentNullException.ThrowIfNull(activos);

        var especialidades = PrepararAsignaciones(especialidadIds, nameof(especialidadIds));
        var locales = PrepararAsignaciones(localIds, nameof(localIds));

        LanzarSiRechazada(ReglaDeAsignaciones.Evaluar(
            especialidades,
            locales,
            [],
            [],
            fichaActiva: true,
            activos));

        var ficha = new Profesional { Id = Guid.NewGuid(), StaffAccountId = staffAccountId };
        ficha.Editar(colegiatura, especialidades, locales, activos);
        ficha.Activar(activos);

        return ficha;
    }

    /// <summary>
    /// Cambia el número de colegiatura y las asignaciones. Se permite mientras haya
    /// especialidades o locales inactivos: esas asignaciones se conservan, pero no
    /// se pueden añadir inactivos nuevos. No cambia el estado de la ficha, y una
    /// ficha activa no puede quedarse por debajo del mínimo con el que se activa.
    /// </summary>
    public void Editar(
        string? colegiatura,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds,
        CatalogoActivo activos)
    {
        ArgumentNullException.ThrowIfNull(especialidadIds);
        ArgumentNullException.ThrowIfNull(localIds);
        ArgumentNullException.ThrowIfNull(activos);

        var numero = DatosDeColegiatura.Preparar(colegiatura);
        var especialidades = PrepararAsignaciones(especialidadIds, nameof(especialidadIds));
        var locales = PrepararAsignaciones(localIds, nameof(localIds));

        // Se comprueban las asignaciones nuevas y el mínimo de la ficha activa antes de
        // tocar el estado para que un rechazo no deje cambios parciales.
        LanzarSiRechazada(ReglaDeAsignaciones.Evaluar(
            especialidades,
            locales,
            _especialidadIds,
            _localIds,
            IsActive,
            activos));

        Colegiatura = numero?.Visible;
        ColegiaturaNormalizada = numero?.Normalizada;

        _especialidadIds.Clear();
        _especialidadIds.AddRange(especialidades);
        _localIds.Clear();
        _localIds.AddRange(locales);
    }

    /// <summary>
    /// Reactiva la ficha. Exige al menos una especialidad activa y un local activo:
    /// la activación de ambos vive en el catálogo y en el local, así que el
    /// llamador resuelve qué identificadores siguen activos.
    /// </summary>
    public void Activar(CatalogoActivo activos)
    {
        ArgumentNullException.ThrowIfNull(activos);

        if (!PuedeActivar(activos))
        {
            throw new InvalidOperationException(
                "La ficha necesita al menos una especialidad activa y un local de atención activo.");
        }

        IsActive = true;
    }

    /// <summary>Indica si la ficha cumple los requisitos para estar activa.</summary>
    public bool PuedeActivar(CatalogoActivo activos)
    {
        ArgumentNullException.ThrowIfNull(activos);

        return _especialidadIds.Any(activos.EsEspecialidadActiva)
            && _localIds.Any(activos.EsLocalActivo);
    }

    /// <summary>Desactiva la ficha. Conserva especialidades, locales y cuenta.</summary>
    public void Desactivar() => IsActive = false;

    /// <summary>
    /// Reconstruye la ficha desde el almacén. No aplica las reglas de edición ni la
    /// de activación: la persistencia ya contiene fichas válidas, incluso sin
    /// especialidades activas, y el almacén solo traduce sus propios datos. La clave
    /// de unicidad se deriva del número visible para que nunca se guarden dos valores
    /// incoherentes.
    /// </summary>
    public static Profesional Restaurar(
        Guid id,
        Guid staffAccountId,
        string? colegiatura,
        bool isActive,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds)
    {
        ArgumentNullException.ThrowIfNull(especialidadIds);
        ArgumentNullException.ThrowIfNull(localIds);

        var ficha = new Profesional
        {
            Id = id,
            StaffAccountId = staffAccountId,
            Colegiatura = colegiatura,
            ColegiaturaNormalizada = string.IsNullOrWhiteSpace(colegiatura)
                ? null
                : DatosDeColegiatura.Normalizar(colegiatura),
            IsActive = isActive,
        };

        ficha._especialidadIds.AddRange(especialidadIds.Distinct().OrderBy(especialidadId => especialidadId));
        ficha._localIds.AddRange(localIds.Distinct().OrderBy(localId => localId));

        return ficha;
    }

    private static Guid[] PrepararAsignaciones(IEnumerable<Guid> ids, string parametro)
    {
        var asignaciones = ids.Distinct().OrderBy(asignado => asignado).ToArray();
        if (asignaciones.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Los identificadores asignados deben ser válidos.", parametro);
        }

        return asignaciones;
    }

    /// <summary>
    /// Traduce el rechazo de la regla compartida a la excepción de la entidad. La
    /// aplicación ya la consultó para devolver el motivo por campo; este lanzamiento
    /// es la última defensa del dominio.
    /// </summary>
    private static void LanzarSiRechazada(RechazoDeAsignaciones rechazo)
    {
        switch (rechazo)
        {
            case RechazoDeAsignaciones.EspecialidadInactiva:
                throw new InvalidOperationException("No se puede asignar una especialidad inactiva.");
            case RechazoDeAsignaciones.LocalInactivo:
                throw new InvalidOperationException("No se puede añadir un local de atención inactivo.");
            case RechazoDeAsignaciones.ActivacionIncompleta:
                throw new InvalidOperationException(
                    "Una ficha activa necesita al menos una especialidad activa y un local de atención activo.");
            case RechazoDeAsignaciones.Ninguno:
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Motivo sin traducción.");
        }
    }
}
