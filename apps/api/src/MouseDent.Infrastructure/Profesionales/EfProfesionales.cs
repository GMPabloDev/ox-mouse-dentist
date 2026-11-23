using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MouseDent.Domain.Profesionales;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Profesionales;

/// <summary>
/// Implementa <see cref="IProfesionales"/> con EF Core. La ficha y la
/// sincronización de sus especialidades y locales se guardan en un solo
/// <c>SaveChanges</c>, así que una escritura rechazada no deja datos parciales. El
/// choque de los índices únicos se traduce al resultado del contrato: la cuenta
/// tiene como máximo una ficha y la colegiatura normalizada es única, también
/// entre las fichas inactivas.
/// </summary>
public sealed class EfProfesionales(MouseDentDbContext database) : IProfesionales
{
    public async Task<Profesional?> BuscarAsync(Guid id, CancellationToken cancellationToken)
    {
        var guardado = await Fichas().SingleOrDefaultAsync(ficha => ficha.Id == id, cancellationToken);

        return guardado is null ? null : AFicha(guardado);
    }

    public async Task<Profesional?> BuscarPorCuentaAsync(Guid staffAccountId, CancellationToken cancellationToken)
    {
        var guardado = await Fichas()
            .SingleOrDefaultAsync(ficha => ficha.StaffAccountId == staffAccountId, cancellationToken);

        return guardado is null ? null : AFicha(guardado);
    }

    public Task<bool> ColegiaturaEstaTomadaAsync(
        string colegiaturaNormalizada,
        Guid? excluirProfesionalId,
        CancellationToken cancellationToken) =>
        excluirProfesionalId is null
            ? database.Profesionales.AnyAsync(
                ficha => ficha.ColegiaturaNormalizada == colegiaturaNormalizada,
                cancellationToken)
            : database.Profesionales.AnyAsync(
                ficha => ficha.ColegiaturaNormalizada == colegiaturaNormalizada
                    && ficha.Id != excluirProfesionalId.Value,
                cancellationToken);

    public async Task<ResultadoGuardadoProfesional> GuardarAsync(
        Profesional profesional,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profesional);

        var guardado = await database.Profesionales
            .Include(ficha => ficha.Especialidades)
            .Include(ficha => ficha.Locales)
            .SingleOrDefaultAsync(ficha => ficha.Id == profesional.Id, cancellationToken);

        if (guardado is null)
        {
            guardado = new ProfesionalGuardado { Id = profesional.Id };
            database.Profesionales.Add(guardado);
        }

        guardado.StaffAccountId = profesional.StaffAccountId;
        guardado.Colegiatura = profesional.Colegiatura;
        guardado.ColegiaturaNormalizada = profesional.ColegiaturaNormalizada;
        guardado.IsActive = profesional.IsActive;

        SincronizarEspecialidades(guardado, profesional.EspecialidadIds);
        SincronizarLocales(guardado, profesional.LocalIds);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return ResultadoGuardadoProfesional.Guardado;
        }
        catch (DbUpdateException exception)
        {
            Rechazar(guardado);

            if (EsChoqueDeColegiatura(exception))
            {
                return ResultadoGuardadoProfesional.ColegiaturaDuplicada;
            }

            if (EsChoqueDeCuenta(exception))
            {
                return ResultadoGuardadoProfesional.CuentaYaVinculada;
            }

            throw;
        }
    }

    private IQueryable<ProfesionalGuardado> Fichas() =>
        database.Profesionales
            .AsNoTracking()
            .Include(ficha => ficha.Especialidades)
            .Include(ficha => ficha.Locales);

    private static Profesional AFicha(ProfesionalGuardado guardado) =>
        Profesional.Restaurar(
            guardado.Id,
            guardado.StaffAccountId,
            guardado.Colegiatura,
            guardado.ColegiaturaNormalizada,
            guardado.IsActive,
            guardado.Especialidades.Select(asignada => asignada.EspecialidadId),
            guardado.Locales.Select(asignado => asignado.LocalId));

    private static void SincronizarEspecialidades(
        ProfesionalGuardado guardado,
        IReadOnlyList<Guid> especialidadIds)
    {
        foreach (var asignada in guardado.Especialidades
            .Where(asignada => !especialidadIds.Contains(asignada.EspecialidadId))
            .ToArray())
        {
            guardado.Especialidades.Remove(asignada);
        }

        foreach (var especialidadId in especialidadIds
            .Where(id => guardado.Especialidades.All(asignada => asignada.EspecialidadId != id)))
        {
            guardado.Especialidades.Add(new ProfesionalEspecialidad
            {
                ProfesionalId = guardado.Id,
                EspecialidadId = especialidadId,
            });
        }
    }

    private static void SincronizarLocales(ProfesionalGuardado guardado, IReadOnlyList<Guid> localIds)
    {
        foreach (var asignado in guardado.Locales
            .Where(asignado => !localIds.Contains(asignado.LocalId))
            .ToArray())
        {
            guardado.Locales.Remove(asignado);
        }

        foreach (var localId in localIds
            .Where(id => guardado.Locales.All(asignado => asignado.LocalId != id)))
        {
            guardado.Locales.Add(new ProfesionalLocal
            {
                ProfesionalId = guardado.Id,
                LocalId = localId,
            });
        }
    }

    /// <summary>
    /// La base ya quedó como estaba porque <c>SaveChanges</c> es transaccional; el
    /// contexto se deja sin cambios pendientes para que el rechazo no se guarde en
    /// una escritura posterior.
    /// </summary>
    private void Rechazar(ProfesionalGuardado guardado)
    {
        foreach (var entry in database.ChangeTracker.Entries().ToArray())
        {
            var pertenece =
                entry.Entity is ProfesionalEspecialidad especialidad
                    && especialidad.ProfesionalId == guardado.Id
                || entry.Entity is ProfesionalLocal local && local.ProfesionalId == guardado.Id;

            if (pertenece)
            {
                entry.State = EntityState.Detached;
            }
        }

        database.Entry(guardado).State = EntityState.Detached;
    }

    private static bool EsChoqueDeColegiatura(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite
        && sqlite.Message.Contains("Profesionales.ColegiaturaNormalizada", StringComparison.Ordinal);

    private static bool EsChoqueDeCuenta(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite
        && sqlite.Message.Contains("Profesionales.StaffAccountId", StringComparison.Ordinal);
}
