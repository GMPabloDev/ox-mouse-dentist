using MouseDent.Application.Auth;
using MouseDent.Application.Catalogo;
using MouseDent.Application.Locales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Profesionales;

/// <summary>
/// Casos de uso de administración de la ficha del profesional: consulta, alta con
/// su cuenta, edición, cambio de estado y reenvío de la invitación. Resuelve con el
/// catálogo y con los locales qué asignaciones están activas, decide si el correo
/// crea una cuenta pendiente o vincula una existente, y delega la escritura de
/// cuenta y ficha en <see cref="IAltasDeProfesionales"/>, que la guarda en una sola
/// transacción. La invitación y su envío pertenecen a <see cref="StaffAccessService"/>.
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

    /// <summary>Ficha con ese identificador, con la misma forma que la lista.</summary>
    public Task<ProfesionalResumen?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        listado.BuscarAsync(id, cancellationToken);

    /// <summary>
    /// Cambia la colegiatura y las asignaciones de una ficha. Solo se pueden añadir
    /// especialidades y locales activos; lo ya asignado se conserva aunque se haya
    /// desactivado después. Una ficha activa no puede quedarse por debajo del mínimo
    /// con el que se activa: para vaciarla se desactiva primero.
    /// </summary>
    public async Task<ProfesionalEdicionOutcome> UpdateAsync(
        Guid id,
        ProfesionalEdicionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ficha = await profesionales.BuscarAsync(id, cancellationToken);
        if (ficha is null)
        {
            return ProfesionalEdicionOutcome.Rejected(ProfesionalEdicionStatus.NotFound);
        }

        if (!DatosDeColegiatura.EsValida(command.Colegiatura))
        {
            return ProfesionalEdicionOutcome.Rejected(ProfesionalEdicionStatus.InvalidColegiatura);
        }

        var active = await ActiveCatalogAsync(cancellationToken);
        var especialidadIds = command.EspecialidadIds.Distinct().ToArray();
        var localIds = command.LocalIds.Distinct().ToArray();

        // La regla compartida con el dominio decide con el estado persistido de la ficha y
        // del catálogo. Así la aplicación devuelve el motivo por campo y la entidad solo
        // lanza si la comprobación y la escritura ven estados distintos.
        var rechazo = ReglaDeAsignaciones.Evaluar(
            especialidadIds,
            localIds,
            ficha.EspecialidadIds,
            ficha.LocalIds,
            ficha.IsActive,
            active);

        if (rechazo is not RechazoDeAsignaciones.Ninguno)
        {
            return ProfesionalEdicionOutcome.Rejected(ToEdicionStatus(rechazo));
        }

        ficha.Editar(command.Colegiatura, especialidadIds, localIds, active);

        return ToEdicionOutcome(await profesionales.GuardarAsync(ficha, cancellationToken));
    }

    /// <summary>
    /// Activa o desactiva una ficha. Desactivar conserva las especialidades, los
    /// locales y la cuenta; reactivar exige recuperar al menos una especialidad activa
    /// y un local activo. Pedir el estado que la ficha ya tiene no es una reactivación:
    /// devuelve la ficha guardada sin revalidar el mínimo, así la operación se puede
    /// repetir sin un conflicto que no existe.
    /// </summary>
    public async Task<ProfesionalEstadoOutcome> SetStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var ficha = await profesionales.BuscarAsync(id, cancellationToken);
        if (ficha is null)
        {
            return ProfesionalEstadoOutcome.Rejected(ProfesionalEstadoStatus.NotFound);
        }

        if (ficha.IsActive == isActive)
        {
            return ProfesionalEstadoOutcome.Saved();
        }

        if (isActive)
        {
            var active = await ActiveCatalogAsync(cancellationToken);
            if (!ficha.PuedeActivar(active))
            {
                return ProfesionalEstadoOutcome.Rejected(ProfesionalEstadoStatus.ActivacionIncompleta);
            }

            ficha.Activar(active);
        }
        else
        {
            ficha.Desactivar();
        }

        return ToEstadoOutcome(await profesionales.GuardarAsync(ficha, cancellationToken));
    }

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

        // La ficha nace activa y sin asignaciones actuales que conservar, así que la regla
        // es la misma que la de una edición activa.
        var rechazo = ReglaDeAsignaciones.Evaluar(
            especialidadIds,
            localIds,
            [],
            [],
            fichaActiva: true,
            active);

        if (rechazo is not RechazoDeAsignaciones.Ninguno)
        {
            return ProfesionalAltaOutcome.Failed(ToAltaStatus(rechazo));
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
            ? ProfesionalAltaOutcome.Saved(ficha)
            : ProfesionalAltaOutcome.InvitationFailed(ficha);
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
            ? ProfesionalAltaOutcome.Saved(ficha)
            : ProfesionalAltaOutcome.Failed(ToStatus(result));
    }

    private async Task<CatalogoActivo> ActiveCatalogAsync(CancellationToken cancellationToken)
    {
        var especialidades = await catalogo.ListEspecialidadesAsync(cancellationToken);
        var localesGuardados = await locales.ListAsync(cancellationToken);

        return new CatalogoActivo(
            especialidades.Where(especialidad => especialidad.IsActive).Select(especialidad => especialidad.Id),
            localesGuardados.Where(local => local.IsActive).Select(local => local.Id));
    }

    /// <summary>Traduce el rechazo de la regla al motivo de la edición.</summary>
    private static ProfesionalEdicionStatus ToEdicionStatus(RechazoDeAsignaciones rechazo) => rechazo switch
    {
        RechazoDeAsignaciones.EspecialidadInactiva => ProfesionalEdicionStatus.EspecialidadInactiva,
        RechazoDeAsignaciones.LocalInactivo => ProfesionalEdicionStatus.LocalInactivo,
        RechazoDeAsignaciones.ActivacionIncompleta => ProfesionalEdicionStatus.ActivacionIncompleta,
        _ => throw new InvalidOperationException("La regla de asignaciones no rechazó la edición."),
    };

    /// <summary>Traduce el rechazo de la regla al motivo del alta.</summary>
    private static ProfesionalAltaStatus ToAltaStatus(RechazoDeAsignaciones rechazo) => rechazo switch
    {
        RechazoDeAsignaciones.EspecialidadInactiva => ProfesionalAltaStatus.EspecialidadInactiva,
        RechazoDeAsignaciones.LocalInactivo => ProfesionalAltaStatus.LocalInactivo,
        RechazoDeAsignaciones.ActivacionIncompleta => ProfesionalAltaStatus.ActivacionIncompleta,
        _ => throw new InvalidOperationException("La regla de asignaciones no rechazó el alta."),
    };

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

    /// <summary>
    /// Traduce el rechazo de la escritura de la edición. La cuenta ya estaba vinculada
    /// y la edición no la toca, así que ese rechazo no puede ocurrir.
    /// </summary>
    private static ProfesionalEdicionOutcome ToEdicionOutcome(ResultadoGuardadoProfesional result) =>
        result switch
        {
            ResultadoGuardadoProfesional.Guardado => ProfesionalEdicionOutcome.Saved(),
            ResultadoGuardadoProfesional.ColegiaturaDuplicada => ProfesionalEdicionOutcome.Rejected(
                ProfesionalEdicionStatus.DuplicateColegiatura),
            ResultadoGuardadoProfesional.EspecialidadInactiva => ProfesionalEdicionOutcome.Rejected(
                ProfesionalEdicionStatus.EspecialidadInactiva),
            ResultadoGuardadoProfesional.LocalInactivo => ProfesionalEdicionOutcome.Rejected(
                ProfesionalEdicionStatus.LocalInactivo),
            ResultadoGuardadoProfesional.ActivacionIncompleta => ProfesionalEdicionOutcome.Rejected(
                ProfesionalEdicionStatus.ActivacionIncompleta),
            _ => throw new InvalidOperationException(
                "La edición conserva la cuenta de la ficha: no hay vínculo que traducir."),
        };

    /// <summary>
    /// Traduce el rechazo de la escritura del estado. El cambio solo toca el estado y
    /// conserva las asignaciones, así que solo puede chocar con el mínimo de activación.
    /// </summary>
    private static ProfesionalEstadoOutcome ToEstadoOutcome(ResultadoGuardadoProfesional result) =>
        result switch
        {
            ResultadoGuardadoProfesional.Guardado => ProfesionalEstadoOutcome.Saved(),
            ResultadoGuardadoProfesional.ActivacionIncompleta => ProfesionalEstadoOutcome.Rejected(
                ProfesionalEstadoStatus.ActivacionIncompleta),
            _ => throw new InvalidOperationException(
                "El cambio de estado conserva las asignaciones de la ficha."),
        };
}
