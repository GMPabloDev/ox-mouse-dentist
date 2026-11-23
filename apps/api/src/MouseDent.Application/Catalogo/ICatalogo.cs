using MouseDent.Domain.Catalogo;

namespace MouseDent.Application.Catalogo;

public interface ICatalogo
{
    Task<IReadOnlyList<Especialidad>> ListEspecialidadesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Servicio>> ListServiciosAsync(CancellationToken cancellationToken);
    Task<Especialidad?> FindEspecialidadAsync(Guid id, CancellationToken cancellationToken);
    Task<Servicio?> FindServicioAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> HasActiveServicesAsync(Guid especialidadId, CancellationToken cancellationToken);
    Task<CatalogoSaveResult> SaveAsync(Especialidad especialidad, CancellationToken cancellationToken);
    Task<CatalogoSaveResult> SaveAsync(Servicio servicio, CancellationToken cancellationToken);
}

public enum CatalogoSaveResult
{
    Saved,
    DuplicateName,
    EspecialidadHasActiveServices,
    DestinationInactive,
}
