using MouseDent.Application.Locales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Auth;

/// <summary>
/// Caso de uso de gestión de cuentas del personal. Decide qué asignaciones son
/// válidas con el estado actual de los locales y qué cambios de perfil permite
/// la ficha de profesional de la cuenta, y delega en el almacén la escritura y la
/// protección del último administrador, que necesita una transacción. La
/// invitación y su envío pertenecen a <see cref="StaffAccessService"/>.
/// </summary>
public sealed class StaffManagementService(
    IStaffAccounts accounts,
    ILocales locales,
    IProfesionales profesionales,
    StaffAccessService access)
{
    public Task<IReadOnlyList<StaffAccount>> ListAsync(string? search, CancellationToken cancellationToken) =>
        accounts.ListAsync(search, cancellationToken);

    public async Task<StaffOutcome> CreateAsync(
        StaffAccountDetails details,
        CancellationToken cancellationToken)
    {
        // El perfil Profesional nace del alta de profesionales, que crea la cuenta
        // y su ficha juntas. Personal solo crea administración y recepción.
        if (details.Profile == StaffRole.Professional)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.ProfessionalProfileNotAllowed);
        }

        var assignments = await ResolveAssignmentsAsync(
            details,
            currentLocalIds: [],
            requireLocal: true,
            cancellationToken);

        if (assignments.Status is { } rejected)
        {
            return StaffOutcome.Failed(rejected);
        }

        var account = StaffAccount.Invite(
            details.DisplayName,
            details.Email,
            details.Phone,
            details.Profile,
            assignments.LocalIds);

        var outcome = ToOutcome(await accounts.CreateAsync(account, cancellationToken), account);

        // La cuenta ya existe pendiente: si el correo falla se conserva y
        // administración puede reenviar la invitación sin crear otra.
        return outcome.Status is StaffOutcomeStatus.Saved
            ? await InviteAsync(account, cancellationToken)
            : outcome;
    }

    public async Task<StaffOutcome> UpdateAsync(
        Guid id,
        StaffAccountDetails details,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.NotFound);
        }

        // Solo se bloquea el cambio de perfil. Editar el nombre, el correo o el
        // teléfono de una cuenta Profesional con su mismo perfil es el único
        // camino para corregir los datos que la ficha muestra desde la cuenta.
        var profileChanged = !string.Equals(account.Profile, details.Profile, StringComparison.Ordinal);

        if (profileChanged && details.Profile == StaffRole.Professional)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.ProfessionalProfileNotAllowed);
        }

        if (profileChanged && await profesionales.BuscarPorCuentaAsync(account.Id, cancellationToken) is not null)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.ProfileLockedByProfessionalFicha);
        }

        var expected = Snapshot(account);
        var assignments = await ResolveAssignmentsAsync(
            details, account.AssignedLocalIds, requireLocal: false, cancellationToken);
        if (assignments.Status is { } rejected)
        {
            return StaffOutcome.Failed(rejected);
        }

        account.UpdateDetails(details.DisplayName, details.Phone, details.Profile, assignments.LocalIds);
        var emailChanged = !string.Equals(account.Email, details.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            account.ChangeEmail(details.Email);
        }

        var result = await accounts.UpdateAsync(account, expected, cancellationToken);
        if (result is StaffSaveResult.Changed)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.ConcurrentChange);
        }

        var outcome = ToOutcome(result, account);
        return emailChanged && outcome.Status is StaffOutcomeStatus.Saved
            ? await InviteAsync(account, cancellationToken)
            : outcome;
    }

    public async Task<StaffOutcome> SetStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var account = await accounts.FindByIdAsync(id, cancellationToken);
            if (account is null)
            {
                return StaffOutcome.Failed(StaffOutcomeStatus.NotFound);
            }

            var expected = Snapshot(account);
            if (isActive)
            {
                account.Activate();
            }
            else
            {
                account.Deactivate();
            }

            var result = await accounts.UpdateAsync(account, expected, cancellationToken);
            if (result is not StaffSaveResult.Changed)
            {
                return ToOutcome(result, account);
            }
        }
    }

    private static StaffAccount Snapshot(StaffAccount account) => StaffAccount.Restore(
        account.Id, account.Email, account.DisplayName, account.Phone, account.Profile,
        account.IsActive, account.PasswordEstablishedAt, account.SessionVersion, account.AssignedLocalIds);

    /// <summary>
    /// Emite una invitación nueva para una cuenta pendiente y activa. El enlace
    /// nuevo invalida el anterior, incluso cuando el envío falla.
    /// </summary>
    public async Task<StaffOutcome> ResendInvitationAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(id, cancellationToken);

        if (account is null)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.NotFound);
        }

        if (!account.IsActive)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.AccountInactive);
        }

        if (!account.IsPending)
        {
            return StaffOutcome.Failed(StaffOutcomeStatus.PasswordAlreadyEstablished);
        }

        return await InviteAsync(account, cancellationToken);
    }

    /// <summary>
    /// Comprueba los locales solicitados. Solo se pueden añadir locales activos:
    /// una asignación que ya existía se conserva aunque el local esté inactivo.
    /// </summary>
    private async Task<AssignmentResolution> ResolveAssignmentsAsync(
        StaffAccountDetails details,
        IReadOnlyList<Guid> currentLocalIds,
        bool requireLocal,
        CancellationToken cancellationToken)
    {
        if (!StaffRole.IsKnown(details.Profile))
        {
            return AssignmentResolution.Rejected(StaffOutcomeStatus.LocalsNotAllowed);
        }

        var requested = details.AssignedLocalIds.Distinct().ToArray();
        if (details.Profile != StaffRole.Reception)
        {
            return requested.Length == 0
                ? AssignmentResolution.Accepted([])
                : AssignmentResolution.Rejected(StaffOutcomeStatus.LocalsNotAllowed);
        }

        if (requireLocal && requested.Length == 0)
        {
            return AssignmentResolution.Rejected(StaffOutcomeStatus.ReceptionRequiresLocal);
        }

        var stored = await locales.ListAsync(cancellationToken);
        var byId = stored.ToDictionary(local => local.Id);

        foreach (var localId in requested)
        {
            if (!byId.TryGetValue(localId, out var local))
            {
                return AssignmentResolution.Rejected(StaffOutcomeStatus.LocalsUnavailable);
            }

            if (!local.IsActive && !currentLocalIds.Contains(localId))
            {
                return AssignmentResolution.Rejected(StaffOutcomeStatus.LocalsUnavailable);
            }
        }

        return AssignmentResolution.Accepted(requested);
    }

    private async Task<StaffOutcome> InviteAsync(StaffAccount account, CancellationToken cancellationToken) =>
        await access.SendInvitationAsync(account, cancellationToken)
            ? StaffOutcome.Saved(account)
            : StaffOutcome.InvitationFailed(account);

    private static StaffOutcome ToOutcome(StaffSaveResult result, StaffAccount account) => result switch
    {
        StaffSaveResult.Saved => StaffOutcome.Saved(account),
        StaffSaveResult.NotFound => StaffOutcome.Failed(StaffOutcomeStatus.NotFound),
        StaffSaveResult.DuplicateEmail => StaffOutcome.Failed(StaffOutcomeStatus.DuplicateEmail),
        StaffSaveResult.LocalsUnavailable => StaffOutcome.Failed(StaffOutcomeStatus.LocalsUnavailable),
        _ => StaffOutcome.Failed(StaffOutcomeStatus.LastAdministrator),
    };

    private sealed record AssignmentResolution(
        IReadOnlyList<Guid> LocalIds,
        StaffOutcomeStatus? Status)
    {
        public static AssignmentResolution Accepted(IReadOnlyList<Guid> localIds) => new(localIds, null);

        public static AssignmentResolution Rejected(StaffOutcomeStatus status) => new([], status);
    }
}
