using MouseDent.Domain.Locales;

namespace MouseDent.Application.Locales;

/// <summary>
/// Puerto hacia el almacén de locales. La implementación con EF Core vive en
/// Infrastructure y el caso de uso no la conoce.
/// </summary>
public interface ILocales
{
    Task<IReadOnlyList<Local>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Devuelve el local rastreado para poder guardar sus cambios después.</summary>
    Task<Local?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Comprobación amable de unicidad. La garantía definitiva es el índice
    /// único de la clave normalizada, que incluye los locales inactivos.
    /// </summary>
    Task<bool> NameIsTakenAsync(
        string normalizedName,
        Guid? excludeLocalId,
        CancellationToken cancellationToken);

    /// <summary>Inserta o actualiza el local según lo que rastree el almacén.</summary>
    Task<LocalSaveResult> SaveAsync(Local local, CancellationToken cancellationToken);
}

public enum LocalSaveResult
{
    Saved,

    /// <summary>La escritura chocó con el índice único del nombre normalizado.</summary>
    DuplicateName,
}
