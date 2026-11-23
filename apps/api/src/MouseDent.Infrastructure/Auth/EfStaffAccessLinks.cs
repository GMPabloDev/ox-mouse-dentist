using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Infrastructure.Persistence;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Implementa <see cref="IStaffAccessLinks"/> con EF Core. Emitir un enlace
/// reemplaza los pendientes de la cuenta y consumirlo comprueba el enlace,
/// aplica la política de contraseña de Identity e invalida las sesiones
/// anteriores en una sola transacción.
/// </summary>
public sealed class EfStaffAccessLinks(
    MouseDentDbContext database,
    UserManager<StaffUser> users,
    TimeProvider clock)
    : IStaffAccessLinks
{
    public async Task<bool> ReplacePendingAsync(StaffAccessLink link, StaffAccount expected, bool requirePending, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var current = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            user => user.Id == link.AccountId, cancellationToken);
        if (current is null || !current.IsActive
            || !string.Equals(current.Email, expected.Email, StringComparison.OrdinalIgnoreCase)
            || current.SessionVersion != expected.SessionVersion
            || (requirePending && current.PasswordEstablishedAt is not null))
        {
            return false;
        }

        await RemovePendingAsync(link.AccountId, cancellationToken);
        database.Add(link);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string tokenHash,
        string password,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var link = await database.Set<StaffAccessLink>()
            .SingleOrDefaultAsync(stored => stored.TokenHash == tokenHash, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        if (link is null || !link.IsUsable(now))
        {
            return PasswordResetResult.LinkUnusable();
        }

        var user = await database.Users
            .SingleOrDefaultAsync(stored => stored.Id == link.AccountId, cancellationToken);

        // Una cuenta desactivada no admite el enlace aunque el registro siga ahí.
        if (user is null || !user.IsActive)
        {
            return PasswordResetResult.LinkUnusable();
        }

        var rejected = await RejectedPasswordCodesAsync(user, password);
        if (rejected.Count > 0)
        {
            return PasswordResetResult.PasswordRejected(rejected);
        }

        // El consumo se reclama en una actualización condicional: si otra solicitud
        // usó el enlace entre la lectura y este punto, no se toca la contraseña.
        var claimed = await database.Set<StaffAccessLink>()
            .Where(stored => stored.Id == link.Id && stored.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(stored => stored.UsedAt, now), cancellationToken);

        if (claimed == 0)
        {
            return PasswordResetResult.LinkUnusable();
        }

        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        user.PasswordEstablishedAt = now;

        // La persona atendió la invitación: el fallo del último envío deja de aplicar.
        user.InvitationFailedAt = null;

        // Completar la contraseña invalida las sesiones anteriores en la siguiente solicitud.
        user.SessionVersion++;

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return PasswordResetResult.Reset();
    }

    /// <summary>El enlace pendiente se elimina; el ya usado no servía y se conserva como registro.</summary>
    private async Task RemovePendingAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var pending = await database.Set<StaffAccessLink>()
            .Where(link => link.AccountId == accountId && link.UsedAt == null)
            .ToListAsync(cancellationToken);

        database.RemoveRange(pending);
    }

    /// <summary>Comprueba la contraseña con los validadores de Identity configurados en la aplicación.</summary>
    private async Task<IReadOnlyList<string>> RejectedPasswordCodesAsync(StaffUser user, string password)
    {
        List<string> rejected = [];

        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            if (!result.Succeeded)
            {
                rejected.AddRange(result.Errors.Select(error => error.Code));
            }
        }

        return rejected;
    }
}
