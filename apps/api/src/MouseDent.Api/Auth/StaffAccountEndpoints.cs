using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MouseDent.Api.Common;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;

namespace MouseDent.Api.Auth;

/// <summary>
/// Operaciones de consulta y gestión de cuentas del personal. Todas exigen el
/// perfil <see cref="StaffRole.Admin"/>; administración no necesita asignaciones
/// por local. Las sesiones y los permisos se comprueban en cada solicitud.
/// </summary>
public static class StaffAccountEndpoints
{
    public static IEndpointRouteBuilder MapStaffAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/staff")
            .WithTags("Staff")
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin));

        group.MapGet("", ListAsync)
            .WithName("ListStaffAccounts")
            .WithSummary("Lista las cuentas del personal, incluidas las pendientes y las inactivas.")
            .Produces<IReadOnlyList<StaffAccountResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateStaffAccountForm>>()
            .WithName("CreateStaffAccount")
            .WithSummary("Crea una cuenta pendiente y la asigna a sus locales si es recepción.")
            .Produces<StaffAccountResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPut("/{id:guid}", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdateStaffAccountForm>>()
            .WithName("UpdateStaffAccount")
            .WithSummary("Edita el nombre, el correo, el teléfono, el perfil y los locales de una cuenta.")
            .Produces<StaffAccountResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPatch("/{id:guid}/status", SetStatusAsync)
            .WithName("SetStaffAccountStatus")
            .WithSummary("Activa o desactiva una cuenta sin exigir el resto de sus datos.")
            .Produces<StaffAccountResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/invitation", ResendInvitationAsync)
            .WithName("ResendStaffInvitation")
            .WithSummary("Emite una invitación nueva para una cuenta pendiente e invalida la anterior.")
            .Produces<StaffAccountResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<StaffAccountResponse>>> ListAsync(
        [FromQuery] string? search,
        StaffManagementService staff,
        CancellationToken cancellationToken)
    {
        var stored = await staff.ListAsync(search, cancellationToken);

        return TypedResults.Ok<IReadOnlyList<StaffAccountResponse>>([.. stored.Select(ToResponse)]);
    }

    private static async Task<Results<Created<StaffAccountResponse>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        [FromBody] CreateStaffAccountForm form,
        StaffManagementService staff,
        CancellationToken cancellationToken)
    {
        var outcome = await staff.CreateAsync(ToDetails(form), cancellationToken);

        return outcome.Status switch
        {
            StaffOutcomeStatus.Saved => TypedResults.Created(
                $"/api/staff/{outcome.Account!.Id}",
                ToResponse(outcome.Account)),
            StaffOutcomeStatus.InvitationFailed => InvitationFailedProblem(outcome.Account!),
            StaffOutcomeStatus.DuplicateEmail => EmailTakenProblem(),
            StaffOutcomeStatus.LastAdministrator => LastAdministratorProblem(),
            _ => AssignmentsProblem(outcome.Status),
        };
    }

    private static async Task<Results<Ok<StaffAccountResponse>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        Guid id,
        [FromBody] UpdateStaffAccountForm form,
        StaffManagementService staff,
        CancellationToken cancellationToken)
    {
        var outcome = await staff.UpdateAsync(id, ToDetails(form), cancellationToken);

        return outcome.Status switch
        {
            StaffOutcomeStatus.Saved => TypedResults.Ok(ToResponse(outcome.Account!)),
            StaffOutcomeStatus.InvitationFailed => InvitationFailedProblem(outcome.Account!),
            StaffOutcomeStatus.ConcurrentChange => ConcurrentChangeProblem(),
            StaffOutcomeStatus.NotFound => NotFoundProblem(),
            StaffOutcomeStatus.DuplicateEmail => EmailTakenProblem(),
            StaffOutcomeStatus.LastAdministrator => LastAdministratorProblem(),
            _ => AssignmentsProblem(outcome.Status),
        };
    }

    private static async Task<Results<Ok<StaffAccountResponse>, ProblemHttpResult>> ResendInvitationAsync(
        Guid id,
        StaffManagementService staff,
        CancellationToken cancellationToken)
    {
        var outcome = await staff.ResendInvitationAsync(id, cancellationToken);

        return outcome.Status switch
        {
            StaffOutcomeStatus.Saved => TypedResults.Ok(ToResponse(outcome.Account!)),
            StaffOutcomeStatus.InvitationFailed => InvitationFailedProblem(outcome.Account!),
            StaffOutcomeStatus.AccountInactive => AccountInactiveProblem(),
            StaffOutcomeStatus.PasswordAlreadyEstablished => PasswordAlreadyEstablishedProblem(),
            _ => NotFoundProblem(),
        };
    }

    private static async Task<Results<Ok<StaffAccountResponse>, ProblemHttpResult>> SetStatusAsync(
        Guid id,
        [FromBody] StaffStatusRequest request,
        StaffManagementService staff,
        CancellationToken cancellationToken)
    {
        var outcome = await staff.SetStatusAsync(id, request.IsActive, cancellationToken);

        return outcome.Status switch
        {
            StaffOutcomeStatus.Saved => TypedResults.Ok(ToResponse(outcome.Account!)),
            StaffOutcomeStatus.LastAdministrator => LastAdministratorProblem(),
            _ => NotFoundProblem(),
        };
    }

    private static StaffAccountDetails ToDetails(StaffAccountForm form) => new(
        form.DisplayName.Trim(),
        form.Email.Trim(),
        string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim(),
        form.Profile,
        form.AssignedLocalIds ?? []);

    private static StaffAccountResponse ToResponse(StaffAccount account) => new(
        account.Id,
        account.DisplayName,
        account.Email,
        account.Phone,
        account.Profile,
        ToState(account),
        account.AssignedLocalIds);

    private static StaffAccountState ToState(StaffAccount account) =>
        !account.IsActive
            ? StaffAccountState.Inactiva
            : account.IsPending
                ? StaffAccountState.Pendiente
                : StaffAccountState.Activa;

    private static ProblemHttpResult NotFoundProblem() => TypedResults.Problem(
        title: "Cuenta no encontrada",
        detail: "No existe una cuenta del personal con ese identificador.",
        statusCode: StatusCodes.Status404NotFound);

    private static ProblemHttpResult ConcurrentChangeProblem() => TypedResults.Problem(
        title: "Cuenta modificada",
        detail: "La cuenta cambió durante la edición. Revisa los datos actualizados antes de volver a guardar.",
        statusCode: StatusCodes.Status409Conflict);

    private static ProblemHttpResult EmailTakenProblem() => TypedResults.Problem(
        title: "Correo ya registrado",
        detail: "Ese correo ya pertenece a otra cuenta, aunque esté inactiva o pendiente.",
        statusCode: StatusCodes.Status409Conflict);

    private static ProblemHttpResult LastAdministratorProblem() => TypedResults.Problem(
        title: "Último administrador disponible",
        detail: "La operación dejaría el sistema sin un administrador activo con contraseña establecida.",
        statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// La cuenta se guardó, pero el correo no salió. Se informa el fallo con el
    /// identificador para que administración pueda reenviar la invitación.
    /// </summary>
    private static ProblemHttpResult InvitationFailedProblem(StaffAccount account) => TypedResults.Problem(
        title: "Invitación sin enviar",
        detail: "La cuenta se guardó, pero el correo con el enlace no se pudo enviar. Revisa la configuración del correo y vuelve a enviar la invitación.",
        statusCode: StatusCodes.Status502BadGateway,
        extensions: new Dictionary<string, object?> { ["accountId"] = account.Id });

    private static ProblemHttpResult AccountInactiveProblem() => TypedResults.Problem(
        title: "Cuenta inactiva",
        detail: "Reactiva la cuenta antes de enviarle una invitación.",
        statusCode: StatusCodes.Status409Conflict);

    private static ProblemHttpResult PasswordAlreadyEstablishedProblem() => TypedResults.Problem(
        title: "Contraseña ya establecida",
        detail: "La cuenta ya estableció su contraseña y no necesita invitación.",
        statusCode: StatusCodes.Status409Conflict);

    private static ValidationProblem AssignmentsProblem(StaffOutcomeStatus status) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(StaffAccountForm.AssignedLocalIds)] =
            [
                status switch
                {
                    StaffOutcomeStatus.ReceptionRequiresLocal =>
                        "Una cuenta de recepción requiere al menos un local activo.",
                    StaffOutcomeStatus.LocalsNotAllowed =>
                        "Solo las cuentas de recepción se asignan a locales.",
                    _ => "Solo puedes asignar locales activos existentes.",
                },
            ],
        });
}
