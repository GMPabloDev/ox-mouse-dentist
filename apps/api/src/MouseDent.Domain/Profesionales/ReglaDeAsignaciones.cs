namespace MouseDent.Domain.Profesionales;

/// <summary>
/// Motivo por el que unas asignaciones no cumplen las reglas de la ficha. El orden
/// de la enumeración es el orden en que se comprueban: primero la actividad de un
/// destino nuevo y después el mínimo de una ficha activa.
/// </summary>
public enum RechazoDeAsignaciones
{
    /// <summary>Las asignaciones pedidas cumplen las reglas.</summary>
    Ninguno,

    /// <summary>Se intenta añadir una especialidad que no está activa en el catálogo.</summary>
    EspecialidadInactiva,

    /// <summary>Se intenta añadir un local que no está activo.</summary>
    LocalInactivo,

    /// <summary>La ficha quedaría activa sin una especialidad activa o sin un local activo.</summary>
    ActivacionIncompleta,
}

/// <summary>
/// Única definición de las reglas de asignación de una ficha: un destino nuevo debe
/// estar activo y una ficha activa no puede quedarse sin una especialidad activa ni
/// sin un local activo. La usan la creación, la edición del dominio y el caso de uso,
/// que la consulta antes de escribir para devolver el rechazo por campo. Una
/// asignación que ya tenía la ficha se conserva aunque su especialidad o su local se
/// hayan desactivado después.
/// </summary>
public static class ReglaDeAsignaciones
{
    public static RechazoDeAsignaciones Evaluar(
        IReadOnlyCollection<Guid> especialidadIds,
        IReadOnlyCollection<Guid> localIds,
        IReadOnlyCollection<Guid> especialidadesActuales,
        IReadOnlyCollection<Guid> localesActuales,
        bool fichaActiva,
        CatalogoActivo activos)
    {
        ArgumentNullException.ThrowIfNull(especialidadIds);
        ArgumentNullException.ThrowIfNull(localIds);
        ArgumentNullException.ThrowIfNull(especialidadesActuales);
        ArgumentNullException.ThrowIfNull(localesActuales);
        ArgumentNullException.ThrowIfNull(activos);

        if (especialidadIds.Any(id =>
            !especialidadesActuales.Contains(id) && !activos.EsEspecialidadActiva(id)))
        {
            return RechazoDeAsignaciones.EspecialidadInactiva;
        }

        if (localIds.Any(id => !localesActuales.Contains(id) && !activos.EsLocalActivo(id)))
        {
            return RechazoDeAsignaciones.LocalInactivo;
        }

        // Una ficha activa mantiene el mínimo con el que se activó. Para vaciarla se
        // desactiva antes. Una ficha inactiva sí puede quedarse sin asignaciones activas.
        return fichaActiva
            && (!especialidadIds.Any(activos.EsEspecialidadActiva) || !localIds.Any(activos.EsLocalActivo))
            ? RechazoDeAsignaciones.ActivacionIncompleta
            : RechazoDeAsignaciones.Ninguno;
    }
}
