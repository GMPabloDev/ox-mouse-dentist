using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MouseDent.Domain.Profesionales;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Profesionales;

/// <summary>
/// Implementa <see cref="IProfesionales"/> con EF Core. La ficha y la
/// sincronización de sus especialidades y locales se guardan en un solo
/// <c>SaveChanges</c>, así que una escritura rechazada no deja datos parciales. El
/// choque de los índices únicos y el de los triggers de escritura se traduce al
/// resultado del contrato: la cuenta tiene como máximo una ficha, la colegiatura
/// normalizada es única, también entre las fichas inactivas, y una ficha activa no
/// se queda sin una especialidad activa ni sin un local activo.
/// </summary>
public sealed class EfProfesionales(MouseDentDbContext database) : IProfesionales
{
    // Textos que produce la base y únicos que conoce este archivo. Los índices viven en
    // la migración 20261123194253_Profesional y los triggers en
    // 20261123201958_ProfesionalReglasDeEscritura y 20261124040050_ProfesionalBorradoGuardado;
    // SQLite devuelve su nombre o su mensaje dentro del error de restricción. Al cambiar
    // una migración hay que cambiar su constante.
    private const string IndiceColegiaturaUnica = "Profesionales.ColegiaturaNormalizada";
    private const string IndiceCuentaUnica = "Profesionales.StaffAccountId";
    private const string TriggerEspecialidadInactiva = "profesional_especialidad_inactiva";
    private const string TriggerLocalInactivo = "profesional_local_inactivo";
    private const string TriggerActivacionIncompleta = "profesional_activacion_incompleta";

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

    public async Task<ResultadoGuardadoProfesional> GuardarAsync(
        Profesional profesional,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profesional);

        var guardado = await database.Profesionales
            .Include(ficha => ficha.Especialidades)
            .Include(ficha => ficha.Locales)
            .AsSplitQuery()
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

        // Los triggers de borrado comprueban la ficha y sus asignaciones en el instante del
        // DELETE. EF escribe los borrados antes que las altas de la misma tabla, así que
        // cambiar la única especialidad activa por otra activa parecería un borrado que deja
        // la ficha sin mínimo. Se escriben primero las altas y los cambios de la ficha y
        // después los borrados, dentro de una sola transacción: el orden es el que esperan
        // los triggers y una operación rechazada no deja cambios parciales.
        var retiradas = database.ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Deleted
                && entry.Entity is ProfesionalEspecialidad or ProfesionalLocal)
            .Select(entry => entry.Entity)
            .ToArray();

        IDbContextTransaction? propia = null;
        try
        {
            foreach (var retirada in retiradas)
            {
                database.Entry(retirada).State = EntityState.Detached;
            }

            if (retiradas.Length > 0 && database.Database.CurrentTransaction is null)
            {
                propia = await database.Database.BeginTransactionAsync(cancellationToken);
            }

            await database.SaveChangesAsync(cancellationToken);

            if (retiradas.Length > 0)
            {
                database.RemoveRange(retiradas);
                await database.SaveChangesAsync(cancellationToken);
            }

            if (propia is not null)
            {
                await propia.CommitAsync(cancellationToken);
            }

            return ResultadoGuardadoProfesional.Guardado;
        }
        catch (DbUpdateException exception)
        {
            Rechazar(guardado);

            if (propia is not null)
            {
                await propia.RollbackAsync(cancellationToken);
            }

            if (EsRestriccion(exception, IndiceColegiaturaUnica))
            {
                return ResultadoGuardadoProfesional.ColegiaturaDuplicada;
            }

            if (EsRestriccion(exception, IndiceCuentaUnica))
            {
                return ResultadoGuardadoProfesional.CuentaYaVinculada;
            }

            if (EsRestriccion(exception, TriggerEspecialidadInactiva))
            {
                return ResultadoGuardadoProfesional.EspecialidadInactiva;
            }

            if (EsRestriccion(exception, TriggerLocalInactivo))
            {
                return ResultadoGuardadoProfesional.LocalInactivo;
            }

            if (EsRestriccion(exception, TriggerActivacionIncompleta))
            {
                return ResultadoGuardadoProfesional.ActivacionIncompleta;
            }

            throw;
        }
        finally
        {
            if (propia is not null)
            {
                await propia.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Lectura de la ficha con sus dos colecciones. Se separa en varias consultas
    /// (<c>AsSplitQuery</c>) para no multiplicar las filas por el producto cartesiano
    /// de especialidades y locales, que además dispara el aviso 20504 de EF Core.
    /// </summary>
    private IQueryable<ProfesionalGuardado> Fichas() =>
        database.Profesionales
            .AsNoTracking()
            .Include(ficha => ficha.Especialidades)
            .Include(ficha => ficha.Locales)
            .AsSplitQuery();

    private static Profesional AFicha(ProfesionalGuardado guardado) =>
        Profesional.Restaurar(
            guardado.Id,
            guardado.StaffAccountId,
            guardado.Colegiatura,
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

    private static bool EsRestriccion(DbUpdateException exception, string texto) =>
        exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite
        && sqlite.Message.Contains(texto, StringComparison.Ordinal);
}
