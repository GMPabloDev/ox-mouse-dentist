using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MouseDent.Application.Catalogo;
using MouseDent.Domain.Catalogo;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Catalogo;

public sealed class EfCatalogo(MouseDentDbContext database) : ICatalogo
{
    public async Task<IReadOnlyList<Especialidad>> ListEspecialidadesAsync(CancellationToken cancellationToken) =>
        await database.Especialidades.AsNoTracking().OrderBy(x => x.NormalizedName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Servicio>> ListServiciosAsync(CancellationToken cancellationToken) =>
        await database.Servicios.AsNoTracking().OrderBy(x => x.NormalizedName).ToListAsync(cancellationToken);

    public Task<Especialidad?> FindEspecialidadAsync(Guid id, CancellationToken cancellationToken) =>
        database.Especialidades.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Servicio?> FindServicioAsync(Guid id, CancellationToken cancellationToken) =>
        database.Servicios.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasActiveServicesAsync(Guid especialidadId, CancellationToken cancellationToken) =>
        database.Servicios.AnyAsync(x => x.EspecialidadId == especialidadId && x.IsActive, cancellationToken);

    public Task<CatalogoSaveResult> SaveAsync(Especialidad especialidad, CancellationToken cancellationToken) =>
        SaveEntityAsync(especialidad, cancellationToken);

    public Task<CatalogoSaveResult> SaveAsync(Servicio servicio, CancellationToken cancellationToken) =>
        SaveEntityAsync(servicio, cancellationToken);

    private async Task<CatalogoSaveResult> SaveEntityAsync<T>(T entity, CancellationToken cancellationToken) where T : class
    {
        if (database.Entry(entity).State == EntityState.Detached)
            database.Set<T>().Add(entity);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return CatalogoSaveResult.Saved;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException sqlite && sqlite.SqliteErrorCode == 19)
        {
            var message = exception.InnerException.Message;
            // Una operación rechazada no debe quedar pendiente para un guardado posterior.
            database.Entry(entity).State = EntityState.Detached;
            if (message.Contains("catalogo_especialidad_con_servicios", StringComparison.Ordinal))
                return CatalogoSaveResult.EspecialidadHasActiveServices;
            if (message.Contains("catalogo_destino_inactivo", StringComparison.Ordinal))
                return CatalogoSaveResult.DestinationInactive;
            if (message.Contains("Especialidades.NormalizedName", StringComparison.Ordinal) ||
                message.Contains("Servicios.EspecialidadId, Servicios.NormalizedName", StringComparison.Ordinal))
                return CatalogoSaveResult.DuplicateName;
            throw;
        }
    }
}
