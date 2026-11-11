using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MouseDent.Api.Common;
using MouseDent.Application.Locales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;

namespace MouseDent.Api.Locales;

/// <summary>
/// Operaciones de consulta y gestión de locales. Todas exigen el perfil
/// <see cref="StaffRole.Admin"/>, incluida la consulta de la foto.
/// </summary>
public static class LocalEndpoints
{
    public static IEndpointRouteBuilder MapLocalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/locales")
            .WithTags("Locales")
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin));

        group.MapGet("", ListAsync)
            .WithName("ListLocales")
            .WithSummary("Lista los locales activos e inactivos con su horario y el estado de la foto.")
            .Produces<IReadOnlyList<LocalResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}/photo", GetPhotoAsync)
            .WithName("GetLocalPhoto")
            .WithSummary("Devuelve la foto de un local.")
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "image/jpeg", "image/png", "image/webp")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", CreateAsync)
            .DisableAntiforgery()
            .AddEndpointFilter<ValidationFilter<CreateLocalForm>>()
            .Accepts<CreateLocalForm>("multipart/form-data")
            .WithName("CreateLocal")
            .WithSummary("Registra un local con su horario semanal y una foto opcional.")
            .Produces<LocalResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateAsync)
            .DisableAntiforgery()
            .AddEndpointFilter<ValidationFilter<UpdateLocalForm>>()
            .Accepts<UpdateLocalForm>("multipart/form-data")
            .WithName("UpdateLocal")
            .WithSummary("Edita los datos, el horario y la foto de un local.")
            .Produces<LocalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", SetStatusAsync)
            .WithName("SetLocalStatus")
            .WithSummary("Activa o desactiva un local sin exigir el resto de los datos.")
            .Produces<LocalResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<LocalResponse>>> ListAsync(
        LocalManagementService locals,
        CancellationToken cancellationToken)
    {
        var stored = await locals.ListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<LocalResponse>>([.. stored.Select(ToResponse)]);
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetPhotoAsync(
        Guid id,
        LocalManagementService locals,
        CancellationToken cancellationToken)
    {
        var photo = await locals.OpenPhotoAsync(id, cancellationToken);
        if (photo is null)
        {
            return TypedResults.Problem(
                title: "Foto no encontrada",
                detail: "El local no existe o no tiene foto.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.File(photo.Content, photo.ContentType);
    }

    private static async Task<Results<Created<LocalResponse>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        [FromForm] CreateLocalForm form,
        LocalManagementService locals,
        CancellationToken cancellationToken)
    {
        var schedule = LocalScheduleReader.Read(form.Schedule);
        if (schedule.Schedule is not { } weeklySchedule)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(schedule.Errors));
        }

        var outcome = await locals.CreateAsync(
            new CreateLocalCommand(ToDetails(form, weeklySchedule), ToUpload(form.Photo)),
            cancellationToken);

        return outcome.Status switch
        {
            LocalOutcomeStatus.Saved => TypedResults.Created(
                $"/api/locales/{outcome.Local!.Id}",
                ToResponse(outcome.Local)),
            _ => DuplicateNameProblem(),
        };
    }

    private static async Task<Results<Ok<LocalResponse>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        Guid id,
        [FromForm] UpdateLocalForm form,
        LocalManagementService locals,
        CancellationToken cancellationToken)
    {
        var schedule = LocalScheduleReader.Read(form.Schedule);
        if (schedule.Schedule is not { } weeklySchedule)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(schedule.Errors));
        }

        if (!LocalPhotoActionParser.TryParse(form.PhotoAction, out var photoAction))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["PhotoAction"] = ["Indica si conservas, sustituyes o quitas la foto con keep, replace o remove."],
            });
        }

        var upload = photoAction == LocalPhotoChange.Replace ? ToUpload(form.Photo) : null;
        var outcome = await locals.UpdateAsync(
            id,
            new UpdateLocalCommand(ToDetails(form, weeklySchedule), photoAction, upload),
            cancellationToken);

        return outcome.Status switch
        {
            LocalOutcomeStatus.Saved => TypedResults.Ok(ToResponse(outcome.Local!)),
            LocalOutcomeStatus.NotFound => NotFoundProblem(),
            _ => DuplicateNameProblem(),
        };
    }

    private static async Task<Results<Ok<LocalResponse>, ProblemHttpResult>> SetStatusAsync(
        Guid id,
        [FromBody] LocalStatusRequest request,
        LocalManagementService locals,
        CancellationToken cancellationToken)
    {
        var outcome = await locals.SetStatusAsync(id, request.IsActive, cancellationToken);

        return outcome.Status switch
        {
            LocalOutcomeStatus.Saved => TypedResults.Ok(ToResponse(outcome.Local!)),
            _ => NotFoundProblem(),
        };
    }

    private static LocalResponse ToResponse(Local local) => new(
        local.Id,
        local.Name,
        local.Address,
        local.Phone,
        local.IsActive,
        [.. local.Schedule.Days.Select(LocalScheduleReader.ToContract)],
        local.Photo is { } photo ? new LocalPhotoSummary(photo.ContentType, photo.FileName) : null);

    private static LocalDetails ToDetails(LocalDataForm form, WeeklySchedule schedule) =>
        new(form.Name.Trim(), form.Address.Trim(), form.Phone.Trim(), schedule);

    private static LocalPhotoUpload? ToUpload(IFormFile? photo) =>
        photo is null
            ? null
            : new LocalPhotoUpload(photo.OpenReadStream(), LocalPhotoRules.Normalize(photo.ContentType));

    private static ProblemHttpResult NotFoundProblem() => TypedResults.Problem(
        title: "Local no encontrado",
        detail: "No existe un local con ese identificador.",
        statusCode: StatusCodes.Status404NotFound);

    private static ProblemHttpResult DuplicateNameProblem() => TypedResults.Problem(
        title: "Nombre de local repetido",
        detail: "Ya existe un local con ese nombre.",
        statusCode: StatusCodes.Status409Conflict);
}
