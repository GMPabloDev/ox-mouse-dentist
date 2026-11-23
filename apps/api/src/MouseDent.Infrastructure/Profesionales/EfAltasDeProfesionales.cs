using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Profesionales;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Profesionales;

/// <summary>
/// Escritura compuesta de cuenta y ficha. Abre una sola transacción, crea la cuenta
/// con <see cref="UserManager{TUser}"/> o comprueba la que se vincula, y guarda la
/// ficha con <see cref="IProfesionales"/> dentro de esa misma transacción. Un
/// rechazo deshace las dos escrituras, así que el alta no deja una cuenta sin ficha
/// ni una ficha sin cuenta.
/// </summary>
public sealed class EfAltasDeProfesionales(
    MouseDentDbContext database,
    UserManager<StaffUser> users,
    IProfesionales profesionales)
    : IAltasDeProfesionales
{
    public async Task<ResultadoEscrituraAlta> CrearConCuentaAsync(
        StaffAccount cuenta,
        Profesional ficha,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(ficha);

        // El alta leyó la cuenta y el catálogo antes de escribir: la transacción
        // empieza sin cambios pendientes de esas lecturas.
        database.ChangeTracker.Clear();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult created;
        try
        {
            // Sin contraseña: la cuenta queda pendiente hasta que la persona use el enlace.
            created = await users.CreateAsync(StaffUserFactory.Nuevo(cuenta));
        }
        catch (DbUpdateException exception) when (SqliteAccountUniqueness.IsDuplicate(exception))
        {
            // Otra alta simultánea insertó el mismo correo entre la lectura y esta escritura.
            return await RejectedAsync(transaction, ResultadoEscrituraAlta.CorreoDuplicado, cancellationToken);
        }

        if (!created.Succeeded)
        {
            return await RejectedAsync(
                transaction,
                IdentityFailure.IsDuplicateEmail(created)
                    ? ResultadoEscrituraAlta.CorreoDuplicado
                    : throw IdentityFailure.Unexpected(created),
                cancellationToken);
        }

        return await SaveFichaAsync(ficha, transaction, cancellationToken);
    }

    public async Task<ResultadoEscrituraAlta> VincularCuentaAsync(
        Profesional ficha,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ficha);

        database.ChangeTracker.Clear();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var cuenta = await database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == ficha.StaffAccountId, cancellationToken);

        if (cuenta is null)
        {
            return await RejectedAsync(transaction, ResultadoEscrituraAlta.CuentaNoEncontrada, cancellationToken);
        }

        if (!string.Equals(cuenta.Profile, StaffRole.Professional, StringComparison.Ordinal))
        {
            return await RejectedAsync(transaction, ResultadoEscrituraAlta.PerfilIncorrecto, cancellationToken);
        }

        if (await database.Profesionales
                .AsNoTracking()
                .AnyAsync(stored => stored.StaffAccountId == ficha.StaffAccountId, cancellationToken))
        {
            return await RejectedAsync(transaction, ResultadoEscrituraAlta.CuentaYaVinculada, cancellationToken);
        }

        return await SaveFichaAsync(ficha, transaction, cancellationToken);
    }

    /// <summary>
    /// Guarda la ficha dentro de la transacción abierta. La cuenta no se vuelve a
    /// tocar: al crear ya está insertada y al vincular se conserva como estaba.
    /// </summary>
    private async Task<ResultadoEscrituraAlta> SaveFichaAsync(
        Profesional ficha,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var saved = await profesionales.GuardarAsync(ficha, cancellationToken);
        if (saved is not ResultadoGuardadoProfesional.Guardado)
        {
            return await RejectedAsync(transaction, ToResult(saved), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ResultadoEscrituraAlta.Guardado;
    }

    /// <summary>
    /// Deshace la transacción y descarta lo rastreado: un rechazo no puede quedar
    /// como cambio pendiente de una escritura posterior del mismo ámbito.
    /// </summary>
    private async Task<ResultadoEscrituraAlta> RejectedAsync(
        IDbContextTransaction transaction,
        ResultadoEscrituraAlta result,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return result;
    }

    private static ResultadoEscrituraAlta ToResult(ResultadoGuardadoProfesional result) => result switch
    {
        ResultadoGuardadoProfesional.ColegiaturaDuplicada => ResultadoEscrituraAlta.ColegiaturaDuplicada,
        ResultadoGuardadoProfesional.CuentaYaVinculada => ResultadoEscrituraAlta.CuentaYaVinculada,
        ResultadoGuardadoProfesional.EspecialidadInactiva => ResultadoEscrituraAlta.EspecialidadInactiva,
        ResultadoGuardadoProfesional.LocalInactivo => ResultadoEscrituraAlta.LocalInactivo,
        ResultadoGuardadoProfesional.ActivacionIncompleta => ResultadoEscrituraAlta.ActivacionIncompleta,
        _ => throw new InvalidOperationException("La ficha se guardó: no hay rechazo que traducir."),
    };
}
