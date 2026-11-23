namespace MouseDent.Domain.Profesionales;

/// <summary>
/// Contrato del almacén de fichas. La implementación vive en Infrastructure y los
/// casos de uso no la conocen. La unicidad no depende de este contrato: el índice
/// único de la cuenta y el de la colegiatura normalizada la sostienen frente a
/// escrituras simultáneas.
/// </summary>
public interface IProfesionales
{
    /// <summary>Devuelve una copia de la ficha, sin rastrear cambios.</summary>
    Task<Profesional?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve la ficha vinculada a la cuenta, o <c>null</c> cuando la cuenta
    /// todavía no tiene ninguna.
    /// </summary>
    Task<Profesional?> BuscarPorCuentaAsync(Guid staffAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// Comprobación amable antes de escribir. La garantía definitiva es el índice
    /// único, que incluye las fichas inactivas.
    /// </summary>
    Task<bool> ColegiaturaEstaTomadaAsync(
        string colegiaturaNormalizada,
        Guid? excluirProfesionalId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inserta o actualiza la ficha y sus asignaciones en una sola operación. Un
    /// rechazo no deja cambios y devuelve el motivo.
    /// </summary>
    Task<ResultadoGuardadoProfesional> GuardarAsync(
        Profesional profesional,
        CancellationToken cancellationToken);
}

/// <summary>Resultado de guardar una ficha. Solo <see cref="Guardado"/> persiste datos.</summary>
public enum ResultadoGuardadoProfesional
{
    Guardado,

    /// <summary>Otra ficha, incluso inactiva, tiene el mismo número de colegiatura.</summary>
    ColegiaturaDuplicada,

    /// <summary>La cuenta ya tiene una ficha vinculada.</summary>
    CuentaYaVinculada,

    /// <summary>La especialidad asignada está inactiva; solo se asignan especialidades activas.</summary>
    EspecialidadInactiva,

    /// <summary>El local asignado está inactivo; solo se añaden locales activos.</summary>
    LocalInactivo,

    /// <summary>La ficha quedaría activa sin una especialidad activa o sin un local activo.</summary>
    ActivacionIncompleta,
}
