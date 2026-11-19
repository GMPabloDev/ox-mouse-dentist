using MouseDent.Application.Locales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;

namespace MouseDent.Application.Auth;

/// <summary>
/// Resuelve el alcance por local de una cuenta con el estado persistido. H-01
/// la usará en sus operaciones de agenda y recepción; esta entrega solo la deja
/// disponible y probada.
/// </summary>
public sealed class LocalAccessService(IStaffAccounts accounts, ILocales locales)
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

        return LocalAccess.AccessibleLocals(account, await locales.ListAsync(cancellationToken));
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

        return local is not null && LocalAccess.CanOperate(account, local);
    }
}
