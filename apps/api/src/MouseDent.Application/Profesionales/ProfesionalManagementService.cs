using MouseDent.Application.Auth;
using MouseDent.Application.Catalogo;
using MouseDent.Application.Locales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Profesionales;

/// <summary>
/// Caso de uso del alta del profesional y del reenvío de su invitación. Resuelve
/// con el catálogo y con los locales qué asignaciones están activas, decide si el
/// correo crea una cuenta pendiente o vincula una existente, y delega la escritura
/// de cuenta y ficha en <see cref="IAltasDeProfesionales"/>, que la guarda en una
/// sola transacción. La invitación y su envío pertenecen a
/// <see cref="StaffAccessService"/>.
/// </summary>
public sealed class ProfesionalManagementService(
    IAltasDeProfesionales altas,
    IProfesionales profesionales,
    IListadoDeProfesionales listado,
    IStaffAccounts accounts,
    ICatalogo catalogo,
    ILocales locales,
    StaffAccessService access)
{
    public Task<IReadOnlyList<ProfesionalResumen>> ListAsync(
        ProfesionalFiltro filtro,
        CancellationToken cancellationToken) =>
        listado.ListAsync(filtro, cancellationToken);

    /// <summary>
    /// Da de alta una ficha con su cuenta. Un correo sin cuenta crea la cuenta
    /// pendiente de perfil <c>Profesional</c>, sin teléfono ni locales, y envía la
    /// invitación; un correo de una cuenta <c>Profesional</c> sin ficha vincula esa
    /// cuenta sin modificarla y sin enviar correo.
    /// </summary>
    public async Task<ProfesionalAltaOutcome> RegisterAsync(
        ProfesionalAltaCommand command,
        CancellationToken cancellationToken)
    {
        if (!DatosDeColegiatura.EsValida(command.Colegiatura))
        {
            return ProfesionalAltaOutcome.Failed(ProfesionalAltaStatus.InvalidColegiatura);
        }

        var active = await ActiveCatalogAsync(cancellationToken);
        var especialidadIds = command.EspecialidadIds.Distinct().ToArray();
        var localIds = command.LocalIds.Distinct().ToArray();

        var rejection = ValidateAssignments(especialidadIds, localIds, active);
        if (rejection is { } rejected)
        {
            return ProfesionalAltaOutcome.Failed(rejected);
        }

        var email = command.Email.Trim();
        var account = await accounts.FindByEmailAsync(email, cancellationToken);

        return account is null
            ? await CreateWithAccountAsync(command, email, especialidadIds, localIds, active, cancellationToken)
            : await LinkAccountAsync(command, account, especialidadIds, localIds, active, cancellationToken);
    }

    /// <summary>
    /// Reenvía la invitación pendiente de una ficha. La ficha debe existir y su
    /// cuenta debe estar activa y pendiente. Registra el fallo del envío en la
    /// cuenta y lo limpia cuando el correo se entrega.
    /// </summary>
    public async Task<ProfesionalInvitationOutcome> ResendInvitationAsync(
        Guid profesionalId,
        CancellationToken cancellationToken)
    {
        var ficha = await profesionales.BuscarAsync(profesionalId, cancellationToken);
        if (ficha is null)
        {
            return ProfesionalInvitationOutcome.Rejected(ProfesionalInvitationStatus.ProfessionalNotFound);
        }

        var account = await accounts.FindByIdAsync(ficha.StaffAccountId, cancellationToken);
        if (account is null)
        {
            return ProfesionalInvitationOutcome.Rejected(ProfesionalInvitationStatus.ProfessionalNotFound);
        }

        if (!account.IsActive)
        {
            return ProfesionalInvitationOutcome.Rejected(ProfesionalInvitationStatus.AccountInactive);
        }

        if (!account.IsPending)
        {
            return ProfesionalInvitationOutcome.Rejected(
                ProfesionalInvitationStatus.PasswordAlreadyEstablished);
        }

        return await access.SendInvitationAsync(account, cancellationToken)
            ? ProfesionalInvitationOutcome.Sent(account)
            : ProfesionalInvitationOutcome.DeliveryFailed(account);
    }

    private async Task<ProfesionalAltaOutcome> CreateWithAccountAsync(
        ProfesionalAltaCommand command,
        string email,
        Guid[] especialidadIds,
        Guid[] localIds,
        CatalogoActivo active,
        CancellationToken cancellationToken)
    {
        // La cuenta nace pendiente, sin teléfono y sin locales: el alta del
        // profesional no asigna locales a la cuenta, solo a la ficha.
        var account = StaffAccount.Invite(
            command.DisplayName,
            email,
            phone: null,
            StaffRole.Professional,
            assignedLocalIds: []);
        var ficha = Profesional.Crear(account.Id, command.Colegiatura, especialidadIds, localIds, active);

        var result = await altas.CrearConCuentaAsync(account, ficha, cancellationToken);
        if (result is not ResultadoEscrituraAlta.Guardado)
        {
            return ProfesionalAltaOutcome.Failed(ToStatus(result));
        }

        // La cuenta y la ficha ya existen: si el correo falla se conserva el alta y
        // administración puede reenviar la invitación desde la ficha.
        return await access.SendInvitationAsync(account, cancellationToken)
            ? ProfesionalAltaOutcome.Saved(ficha, account)
            : ProfesionalAltaOutcome.InvitationFailed(ficha, account);
    }

    private async Task<ProfesionalAltaOutcome> LinkAccountAsync(
        ProfesionalAltaCommand command,
        StaffAccount account,
        Guid[] especialidadIds,
        Guid[] localIds,
        CatalogoActivo active,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(account.Profile, StaffRole.Professional, StringComparison.Ordinal))
        {
            return ProfesionalAltaOutcome.Failed(ProfesionalAltaStatus.WrongProfile);
        }

        if (await profesionales.BuscarPorCuentaAsync(account.Id, cancellationToken) is not null)
        {
            return ProfesionalAltaOutcome.Failed(ProfesionalAltaStatus.AccountAlreadyLinked);
        }

        // El nombre del alta se ignora a propósito: el dato vive en la cuenta y el
        // vínculo no la modifica, igual que su correo, su estado y su contraseña.
        var ficha = Profesional.Crear(account.Id, command.Colegiatura, especialidadIds, localIds, active);

        var result = await altas.VincularCuentaAsync(ficha, cancellationToken);

        return result is ResultadoEscrituraAlta.Guardado
            ? ProfesionalAltaOutcome.Saved(ficha, account)
            : ProfesionalAltaOutcome.Failed(ToStatus(result));
    }

    /// <summary>
    /// Comprueba las asignaciones con el catálogo y los locales del momento. Solo se
    /// asignan especialidades y locales activos, y la ficha nace activa, así que
    /// necesita al menos uno de cada uno. La ficha y la base repiten la regla.
    /// </summary>
    private static ProfesionalAltaStatus? ValidateAssignments(
        Guid[] especialidadIds,
        Guid[] localIds,
        CatalogoActivo active)
    {
        if (especialidadIds.Any(id => !active.EsEspecialidadActiva(id)))
        {
            return ProfesionalAltaStatus.EspecialidadInactiva;
        }

        if (localIds.Any(id => !active.EsLocalActivo(id)))
        {
            return ProfesionalAltaStatus.LocalInactivo;
        }

        return !especialidadIds.Any(active.EsEspecialidadActiva) || !localIds.Any(active.EsLocalActivo)
            ? ProfesionalAltaStatus.ActivacionIncompleta
            : null;
    }

    private async Task<CatalogoActivo> ActiveCatalogAsync(CancellationToken cancellationToken)
    {
        var especialidades = await catalogo.ListEspecialidadesAsync(cancellationToken);
        var localesGuardados = await locales.ListAsync(cancellationToken);

        return new CatalogoActivo(
            especialidades.Where(especialidad => especialidad.IsActive).Select(especialidad => especialidad.Id),
            localesGuardados.Where(local => local.IsActive).Select(local => local.Id));
    }

    /// <summary>Traduce el rechazo de la escritura al motivo que ve administración.</summary>
    private static ProfesionalAltaStatus ToStatus(ResultadoEscrituraAlta result) => result switch
    {
        ResultadoEscrituraAlta.CorreoDuplicado => ProfesionalAltaStatus.DuplicateEmail,
        ResultadoEscrituraAlta.CuentaNoEncontrada => ProfesionalAltaStatus.AccountNotFound,
        ResultadoEscrituraAlta.PerfilIncorrecto => ProfesionalAltaStatus.WrongProfile,
        ResultadoEscrituraAlta.CuentaYaVinculada => ProfesionalAltaStatus.AccountAlreadyLinked,
        ResultadoEscrituraAlta.ColegiaturaDuplicada => ProfesionalAltaStatus.DuplicateColegiatura,
        ResultadoEscrituraAlta.EspecialidadInactiva => ProfesionalAltaStatus.EspecialidadInactiva,
        ResultadoEscrituraAlta.LocalInactivo => ProfesionalAltaStatus.LocalInactivo,
        ResultadoEscrituraAlta.ActivacionIncompleta => ProfesionalAltaStatus.ActivacionIncompleta,
        _ => throw new InvalidOperationException(
            "La escritura del alta solo se traduce cuando la ficha no se guardó."),
    };
}
