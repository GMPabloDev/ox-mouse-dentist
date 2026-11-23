namespace MouseDent.Application.Catalogo;

/// <summary>Estado final de una operación de gestión del catálogo.</summary>
public enum CatalogoOutcomeStatus
{
    Saved,
    NotFound,
    DuplicateName,
    ActiveServices,
    InactiveDestination,
}

/// <summary>Resultado de una operación del catálogo sobre una especialidad o un servicio.</summary>
public sealed record CatalogoOutcome<T>
    where T : class
{
    private CatalogoOutcome(T? entity, CatalogoOutcomeStatus status)
    {
        Entity = entity;
        Status = status;
    }

    /// <summary>Registro guardado. Solo existe cuando el estado es <see cref="CatalogoOutcomeStatus.Saved"/>.</summary>
    public T? Entity { get; }

    public CatalogoOutcomeStatus Status { get; }

    public static CatalogoOutcome<T> Saved(T entity) => new(entity, CatalogoOutcomeStatus.Saved);

    public static CatalogoOutcome<T> NotFound() => new(null, CatalogoOutcomeStatus.NotFound);

    public static CatalogoOutcome<T> DuplicateName() => new(null, CatalogoOutcomeStatus.DuplicateName);

    public static CatalogoOutcome<T> ActiveServices() => new(null, CatalogoOutcomeStatus.ActiveServices);

    public static CatalogoOutcome<T> InactiveDestination() => new(null, CatalogoOutcomeStatus.InactiveDestination);
}
