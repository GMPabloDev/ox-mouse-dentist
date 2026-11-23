using MouseDent.Application.Locales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Auth;

/// <summary>
/// Resuelve el alcance por local de una cuenta con el estado persistido. H-01
/// la usará en sus operaciones de agenda y recepción; esta entrega solo la deja
/// disponible y probada.
/// </summary>
public sealed class LocalAccessService(IStaffAccounts accounts, IProfesionales profesionales, ILocales locales)
{
    /// <summary>Locales donde la cuenta puede operar en este momento.</summary>
    public async Task<IReadOnlyList<Local>> AccessibleLocalsAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return [];
        }

        var ficha = await FindFichaAsync(account, cancellationToken);

        return LocalAccess.AccessibleLocals(account, ficha, await locales.ListAsync(cancellationToken));
    }

    public async Task<bool> CanOperateAsync(
        Guid accountId,
        Guid localId,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return false;
        }

        var local = await locales.FindAsync(localId, cancellationToken);
        if (local is null)
        {
            return false;
        }

        return LocalAccess.CanOperate(account, await FindFichaAsync(account, cancellationToken), local);
    }

    /// <summary>
    /// Lee la ficha de la cuenta en cada llamada, sin cachearla: asignar o retirar
    /// locales, desactivar la ficha o reactivarla se aplica en la siguiente
    /// solicitud. Administración y recepción no tienen ficha que leer.
    /// </summary>
    private Task<Profesional?> FindFichaAsync(StaffAccount account, CancellationToken cancellationToken) =>
        account.Profile == StaffRole.Professional
            ? profesionales.BuscarPorCuentaAsync(account.Id, cancellationToken)
            : Task.FromResult<Profesional?>(null);
}
