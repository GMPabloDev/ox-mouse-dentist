using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MouseDent.Api.Auth;
using MouseDent.Api.Common;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Api.Profesionales;

/// <summary>
/// Operaciones de consulta y gestión de las fichas de los profesionales. Todas
/// exigen el perfil <see cref="StaffRole.Admin"/>: recepción y profesionales no
/// consultan la lista ni la ficha. Los errores de las reglas del alta y de la
/// edición son <see cref="ProblemDetails"/> o <see cref="ValidationProblemDetails"/>.
/// </summary>
public static class ProfesionalEndpoints
{
    /// <summary>
    /// Los dos formularios nombran igual el campo de colegiatura. Una sola constante
    /// evita que el error de la edición dependa del formulario del alta.
    /// </summary>
    private const string ColegiaturaField = nameof(CreateProfesionalForm.Colegiatura);

    public static IEndpointRouteBuilder MapProfesionalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profesionales")
            .WithTags("Profesionales")
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin));

        group.MapGet("", ListAsync)
            .WithName("ListProfesionales")
            .WithSummary("Lista las fichas de los profesionales con el estado de su cuenta y sus asignaciones.")
            .WithDescription(
                "Los parámetros de búsqueda y de filtro quedan para consumidores directos del endpoint. La pantalla de administración carga la lista y filtra en memoria. La búsqueda usa LIKE de SQLite y no ignora tildes: «Lucia» no encuentra «Lucía».")
            .Produces<IReadOnlyList<ProfesionalResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetProfesional")
            .WithSummary("Devuelve la ficha de un profesional con el estado de su cuenta.")
            .Produces<ProfesionalResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateProfesionalForm>>()
            .WithName("CreateProfesional")
            .WithSummary("Crea la ficha con su cuenta pendiente y la invitación, o vincula una cuenta Profesional sin ficha.")
            .Produces<ProfesionalResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithDescription(
                "Errores del alta. La extensión code del ProblemDetails identifica el caso: colegiatura_duplicada, correo_con_ficha, correo_de_otra_cuenta, activacion_incompleta, alta_simultanea o cuenta_no_encontrada en el 409, e invitacion_sin_enviar en el 502, que además trae profesionalId.");

        group.MapPut("/{id:guid}", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdateProfesionalForm>>()
            .WithName("UpdateProfesional")
            .WithSummary("Edita la colegiatura, las especialidades y los locales de la ficha; no cambia los datos de la cuenta.")
            .Produces<ProfesionalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithDescription(
                "Errores de la edición. La extensión code del ProblemDetails identifica el caso: colegiatura_duplicada, correo_con_ficha, correo_de_otra_cuenta o activacion_incompleta.");

        group.MapPatch("/{id:guid}/status", SetStatusAsync)
            .WithName("SetProfesionalStatus")
            .WithSummary("Activa o desactiva la ficha sin borrar sus especialidades, sus locales ni su cuenta.")
            .Produces<ProfesionalResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithDescription(
                "La ficha no recuperó el mínimo. La extensión code del ProblemDetails vale activacion_incompleta.");

        group.MapPost("/{id:guid}/invitation", ResendInvitationAsync)
            .WithName("ResendProfesionalInvitation")
            .WithSummary("Reenvía la invitación pendiente de la cuenta del profesional.")
            .Produces<ProfesionalResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithDescription(
                "La cuenta no admite la invitación si está inactiva o ya estableció su contraseña. Si el enlace se emitió pero el correo no se entregó, el 502 trae la extensión code invitacion_sin_enviar y profesionalId.");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ProfesionalResponse>>> ListAsync(
        [FromQuery] string? search,
        [FromQuery] Guid? especialidadId,
        [FromQuery] Guid? localId,
        [FromQuery] bool? fichaActiva,
        [FromQuery] StaffAccountState? accountStatus,
        ProfesionalManagementService profesionales,
        CancellationToken cancellationToken)
    {
        var stored = await profesionales.ListAsync(
            new ProfesionalFiltro
            {
                Search = search,
                EspecialidadId = especialidadId,
                LocalId = localId,
                FichaActiva = fichaActiva,
                AccountStatus = ToEstadoDeCuenta(accountStatus),
            },
            cancellationToken);

        return TypedResults.Ok<IReadOnlyList<ProfesionalResponse>>([.. stored.Select(ToResponse)]);
    }

    private static async Task<Results<Ok<ProfesionalResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        ProfesionalManagementService profesionales,
        CancellationToken cancellationToken)
    {
        var resumen = await profesionales.GetAsync(id, cancellationToken);

        return resumen is null
            ? NotFoundProblem()
            : TypedResults.Ok(ToResponse(resumen));
    }

    private static async Task<Results<Created<ProfesionalResponse>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        [FromBody] CreateProfesionalForm form,
        ProfesionalManagementService profesionales,
        CancellationToken cancellationToken)
    {
        var outcome = await profesionales.RegisterAsync(
            new ProfesionalAltaCommand(
                form.DisplayName.Trim(),
                form.Email.Trim(),
                form.Colegiatura,
                form.EspecialidadIds ?? [],
                form.LocalIds ?? []),
            cancellationToken);

        return outcome.Status switch
        {
            ProfesionalAltaStatus.Saved => TypedResults.Created(
                $"/api/profesionales/{outcome.Ficha!.Id}",
                await ReadAsync(profesionales, outcome.Ficha.Id, cancellationToken)),
            ProfesionalAltaStatus.InvitationFailed => InvitationFailedProblem(outcome.Ficha!.Id),
            ProfesionalAltaStatus.WrongProfile => WrongProfileProblem(),
            ProfesionalAltaStatus.AccountAlreadyLinked => AccountAlreadyLinkedProblem(),
            ProfesionalAltaStatus.DuplicateEmail => ConcurrentAltaProblem(),
            ProfesionalAltaStatus.AccountNotFound => AccountNotFoundProblem(),
            ProfesionalAltaStatus.InvalidColegiatura => InvalidColegiaturaProblem(),
            ProfesionalAltaStatus.DuplicateColegiatura => DuplicateColegiaturaProblem(),
            _ => AssignmentsProblem(outcome.Status),
        };
    }

    private static async Task<Results<Ok<ProfesionalResponse>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        Guid id,
        [FromBody] UpdateProfesionalForm form,
        ProfesionalManagementService profesionales,
        CancellationToken cancellationToken)
    {
        var outcome = await profesionales.UpdateAsync(
            id,
            new ProfesionalEdicionCommand(
                form.Colegiatura,
                form.EspecialidadIds ?? [],
                form.LocalIds ?? []),
            cancellationToken);

        return outcome.Status switch
        {
            ProfesionalEdicionStatus.Saved => TypedResults.Ok(
                await ReadAsync(profesionales, id, cancellationToken)),
            ProfesionalEdicionStatus.NotFound => NotFoundProblem(),
            ProfesionalEdicionStatus.InvalidColegiatura => InvalidColegiaturaProblem(),
            ProfesionalEdicionStatus.DuplicateColegiatura => DuplicateColegiaturaProblem(),
            _ => AssignmentsProblem(outcome.Status),
        };
    }

    private static async Task<Results<Ok<ProfesionalResponse>, ProblemHttpResult>> SetStatusAsync(
        Guid id,
        [FromBody] ProfesionalStatusRequest request,
        ProfesionalManagementService profesionales,
        CancellationToken cancellationToken)
    {
        var outcome = await profesionales.SetStatusAsync(id, request.IsActive, cancellationToken);

        return outcome.Status switch
        {
            ProfesionalEstadoStatus.Saved => TypedResults.Ok(
                await ReadAsync(profesionales, id, cancellationToken)),
            ProfesionalEstadoStatus.NotFound => NotFoundProblem(),
            _ => ActivationIncompleteProblem(),
        };
    }

    private static async Task<Results<Ok<ProfesionalResponse>, ProblemHttpResult>> ResendInvitationAsync(
        Guid id,
        ProfesionalManagementService profesionales,
        CancellationToken cancellationToken)
    {
        var outcome = await profesionales.ResendInvitationAsync(id, cancellationToken);

        return outcome.Status switch
        {
            ProfesionalInvitationStatus.Sent => TypedResults.Ok(
                await ReadAsync(profesionales, id, cancellationToken)),
            ProfesionalInvitationStatus.DeliveryFailed => InvitationFailedProblem(id),
            ProfesionalInvitationStatus.AccountInactive => AccountInactiveProblem(),
            ProfesionalInvitationStatus.PasswordAlreadyEstablished => PasswordAlreadyEstablishedProblem(),
            _ => NotFoundProblem(),
        };
    }

    /// <summary>
    /// Representación de la ficha guardada. Se lee del modelo de lectura para que el
    /// alta, la edición, el cambio de estado y el reenvío devuelvan la misma forma que
    /// la lista y la ficha.
    /// </summary>
    private static async Task<ProfesionalResponse> ReadAsync(
        ProfesionalManagementService profesionales,
        Guid id,
        CancellationToken cancellationToken)
    {
        var resumen = await profesionales.GetAsync(id, cancellationToken);

        return resumen is null
            ? throw new InvalidOperationException("La ficha guardada no está en el modelo de lectura.")
            : ToResponse(resumen);
    }

    private static ProfesionalResponse ToResponse(ProfesionalResumen resumen) => new(
        resumen.Id,
        resumen.StaffAccountId,
        resumen.DisplayName,
        resumen.Email,
        resumen.Phone,
        resumen.Colegiatura,
        resumen.IsActive,
        ToState(resumen.AccountStatus),
        resumen.InvitationFailed,
        [.. resumen.Especialidades.Select(especialidad =>
            new ProfesionalEspecialidadResponse(especialidad.Id, especialidad.Name, especialidad.IsActive))],
        [.. resumen.Locales.Select(local =>
            new ProfesionalLocalResponse(local.Id, local.Name, local.IsActive))]);

    /// <summary>
    /// El estado visible de la cuenta es el mismo vocabulario que el del personal, así
    /// que la web no traduce dos juegos de estados.
    /// </summary>
    private static StaffAccountState ToState(EstadoDeCuenta estado) => estado switch
    {
        EstadoDeCuenta.Pendiente => StaffAccountState.Pendiente,
        EstadoDeCuenta.Activa => StaffAccountState.Activa,
        EstadoDeCuenta.Inactiva => StaffAccountState.Inactiva,
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "Estado de cuenta sin traducción al contrato."),
    };

    private static EstadoDeCuenta? ToEstadoDeCuenta(StaffAccountState? estado) => estado switch
    {
        null => null,
        StaffAccountState.Pendiente => EstadoDeCuenta.Pendiente,
        StaffAccountState.Activa => EstadoDeCuenta.Activa,
        StaffAccountState.Inactiva => EstadoDeCuenta.Inactiva,
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado),
            estado,
            "Estado de cuenta del filtro sin traducción al dominio."),
    };

    private static ProblemHttpResult NotFoundProblem() => TypedResults.Problem(
        title: "Ficha no encontrada",
        detail: "No existe una ficha de profesional con ese identificador.",
        statusCode: StatusCodes.Status404NotFound);

    private static ProblemHttpResult DuplicateColegiaturaProblem() => TypedResults.Problem(
        title: "Colegiatura duplicada",
        detail: "Otra ficha, incluso inactiva, ya tiene ese número de colegiatura.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: Code(ProfesionalErrorCodes.ColegiaturaDuplicada));

    /// <summary>
    /// El límite se valida en el formulario; esta respuesta cubre un valor que llegue
    /// fuera de él con el mismo error de campo.
    /// </summary>
    private static ValidationProblem InvalidColegiaturaProblem() => TypedResults.ValidationProblem(
        new Dictionary<string, string[]>
        {
            [ColegiaturaField] =
            [
                $"El número de colegiatura no puede superar los {DatosDeColegiatura.LongitudMaxima} caracteres.",
            ],
        });

    /// <summary>
    /// El alta solo vincula cuentas de perfil Profesional. Una cuenta de otro perfil
    /// no se convierte en profesional desde este formulario.
    /// </summary>
    private static ProblemHttpResult WrongProfileProblem() => TypedResults.Problem(
        title: "Correo de otra cuenta",
        detail: "Ese correo pertenece a una cuenta de administración o recepción. El alta de profesionales solo vincula cuentas de perfil Profesional sin ficha.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: Code(ProfesionalErrorCodes.CorreoDeOtraCuenta));

    private static ProblemHttpResult AccountAlreadyLinkedProblem() => TypedResults.Problem(
        title: "Correo con ficha",
        detail: "Ese correo pertenece a una cuenta que ya tiene una ficha de profesional. Edita la ficha existente.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: Code(ProfesionalErrorCodes.CorreoConFicha));

    /// <summary>Otra alta simultánea tomó el correo entre la lectura y la escritura.</summary>
    private static ProblemHttpResult ConcurrentAltaProblem() => TypedResults.Problem(
        title: "Alta simultánea",
        detail: "Otra solicitud registró ese correo al mismo tiempo. Revisa la lista de profesionales y vuelve a intentarlo.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: Code(ProfesionalErrorCodes.AltaSimultanea));

    /// <summary>
    /// La cuenta a vincular desapareció entre la lectura y la escritura, así que el
    /// vínculo no encontró a quién vincular. No es un correo repetido: la solicitud sí
    /// leyó la cuenta y el alta no compitió con otra.
    /// </summary>
    private static ProblemHttpResult AccountNotFoundProblem() => TypedResults.Problem(
        title: "Cuenta no encontrada",
        detail: "La cuenta de ese correo dejó de existir entre la lectura y el guardado de la ficha. Revisa la lista de personal y vuelve a intentarlo.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: Code(ProfesionalErrorCodes.CuentaNoEncontrada));

    /// <summary>
    /// La ficha y la cuenta quedaron guardadas, pero el correo no salió. Se informa el
    /// fallo con el identificador para que administración pueda reenviar la invitación
    /// desde la ficha, sin volver a darla de alta.
    /// </summary>
    private static ProblemHttpResult InvitationFailedProblem(Guid profesionalId) => TypedResults.Problem(
        title: "Invitación sin enviar",
        detail: "La ficha y la cuenta del profesional quedaron guardadas, pero el correo con el enlace no se pudo enviar. Revisa la configuración del correo y vuelve a enviar la invitación desde la ficha.",
        statusCode: StatusCodes.Status502BadGateway,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = ProfesionalErrorCodes.InvitacionSinEnviar,
            ["profesionalId"] = profesionalId,
        });

    private static ProblemHttpResult AccountInactiveProblem() => TypedResults.Problem(
        title: "Cuenta inactiva",
        detail: "Reactiva la cuenta del profesional en el módulo de personal antes de enviarle una invitación.",
        statusCode: StatusCodes.Status409Conflict);

    private static ProblemHttpResult PasswordAlreadyEstablishedProblem() => TypedResults.Problem(
        title: "Contraseña ya establecida",
        detail: "La cuenta del profesional ya estableció su contraseña y no necesita invitación.",
        statusCode: StatusCodes.Status409Conflict);

    /// <summary>La ficha no recuperó al menos una especialidad activa y un local activo.</summary>
    private static ProblemHttpResult ActivationIncompleteProblem() => TypedResults.Problem(
        title: "Activación incompleta",
        detail: "La ficha necesita al menos una especialidad activa y un local de atención activo. Asigna una especialidad y un local activos antes de reactivarla.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: Code(ProfesionalErrorCodes.ActivacionIncompleta));

    /// <summary>
    /// Traduce una asignación rechazada al campo que administración debe corregir. El
    /// motivo depende del estado persistido del catálogo y del local, por eso se resuelve
    /// en el caso de uso y aquí solo se publica. Solo se marca el campo afectado: el que
    /// llegó activo y existente no tiene nada que corregir.
    /// </summary>
    private static ValidationProblem AssignmentsProblem(ProfesionalAltaStatus status) => status switch
    {
        ProfesionalAltaStatus.EspecialidadInactiva => SingleFieldProblem(
            nameof(CreateProfesionalForm.EspecialidadIds),
            "Solo puedes asignar especialidades activas existentes."),
        ProfesionalAltaStatus.LocalInactivo => SingleFieldProblem(
            nameof(CreateProfesionalForm.LocalIds),
            "Solo puedes añadir locales de atención activos existentes."),
        ProfesionalAltaStatus.ActivacionIncompleta => TypedResults.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [nameof(CreateProfesionalForm.EspecialidadIds)] =
                [
                    "La ficha activa necesita al menos una especialidad activa.",
                ],
                [nameof(CreateProfesionalForm.LocalIds)] =
                [
                    "La ficha activa necesita al menos un local de atención activo.",
                ],
            }),
        _ => throw new InvalidOperationException(
            "Solo los motivos de asignación tienen un campo que corregir."),
    };

    private static ValidationProblem AssignmentsProblem(ProfesionalEdicionStatus status) => status switch
    {
        ProfesionalEdicionStatus.EspecialidadInactiva => SingleFieldProblem(
            nameof(UpdateProfesionalForm.EspecialidadIds),
            "Solo puedes asignar especialidades activas existentes; las ya asignadas se conservan."),
        ProfesionalEdicionStatus.LocalInactivo => SingleFieldProblem(
            nameof(UpdateProfesionalForm.LocalIds),
            "Solo puedes añadir locales de atención activos existentes; los ya asignados se conservan."),
        ProfesionalEdicionStatus.ActivacionIncompleta => TypedResults.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [nameof(UpdateProfesionalForm.EspecialidadIds)] =
                [
                    "La ficha activa necesita al menos una especialidad activa. Desactívala antes de retirarlas todas o asigna una activa.",
                ],
                [nameof(UpdateProfesionalForm.LocalIds)] =
                [
                    "La ficha activa necesita al menos un local de atención activo. Desactívala antes de retirarlos todos o asigna uno activo.",
                ],
            }),
        _ => throw new InvalidOperationException(
            "Solo los motivos de asignación tienen un campo que corregir."),
    };

    private static ValidationProblem SingleFieldProblem(string campo, string mensaje) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [campo] = [mensaje] });

    /// <summary>Extensión estable que la web usa para clasificar el conflicto.</summary>
    private static Dictionary<string, object?> Code(string code) => new() { ["code"] = code };
}
