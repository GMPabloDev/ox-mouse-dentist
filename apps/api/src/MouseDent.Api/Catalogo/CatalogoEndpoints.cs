using Microsoft.AspNetCore.Mvc;
using MouseDent.Api.Common;
using MouseDent.Application.Catalogo;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;

namespace MouseDent.Api.Catalogo;

/// <summary>
/// Consulta y gestión del catálogo. Todo el grupo exige sesión; recepción y
/// profesionales solo consultan y las escrituras exigen <see cref="StaffRole.Admin"/>.
/// </summary>
public static class CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalogo")
            .WithTags("Catalogo")
            .RequireAuthorization(policy => policy.RequireRole(
                StaffRole.Admin,
                StaffRole.Reception,
                StaffRole.Professional));

        group.MapGet("/especialidades", ListEspecialidadesAsync)
            .WithName("ListEspecialidades")
            .Produces<IReadOnlyList<EspecialidadResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/servicios", ListServiciosAsync)
            .WithName("ListServicios")
            .Produces<IReadOnlyList<ServicioResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/especialidades", CreateEspecialidadAsync)
            .AddEndpointFilter<ValidationFilter<EspecialidadRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin))
            .WithName("CreateEspecialidad")
            .Produces<EspecialidadResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/especialidades/{id:guid}", UpdateEspecialidadAsync)
            .AddEndpointFilter<ValidationFilter<EspecialidadRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin))
            .WithName("UpdateEspecialidad")
            .Produces<EspecialidadResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/especialidades/{id:guid}/status", StatusEspecialidadAsync)
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin))
            .WithName("SetEspecialidadStatus")
            .Produces<EspecialidadResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/servicios", CreateServicioAsync)
            .AddEndpointFilter<ValidationFilter<ServicioRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin))
            .WithName("CreateServicio")
            .Produces<ServicioResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/servicios/{id:guid}", UpdateServicioAsync)
            .AddEndpointFilter<ValidationFilter<ServicioRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin))
            .WithName("UpdateServicio")
            .Produces<ServicioResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/servicios/{id:guid}/status", StatusServicioAsync)
            .RequireAuthorization(policy => policy.RequireRole(StaffRole.Admin))
            .WithName("SetServicioStatus")
            .Produces<ServicioResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListEspecialidadesAsync(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        HttpContext context,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var stored = await catalogo.EspecialidadesAsync(
            search,
            isActive,
            context.User.IsInRole(StaffRole.Admin),
            cancellationToken);

        return Results.Ok(stored.Select(ToResponse).ToList());
    }

    private static async Task<IResult> ListServiciosAsync(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? especialidadId,
        HttpContext context,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var stored = await catalogo.ServiciosAsync(
            search,
            isActive,
            especialidadId,
            context.User.IsInRole(StaffRole.Admin),
            cancellationToken);

        return Results.Ok(stored.Select(ToResponse).ToList());
    }

    private static async Task<IResult> CreateEspecialidadAsync(
        EspecialidadRequest request,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var outcome = await catalogo.SaveEspecialidadAsync(
            null,
            request.Name,
            request.Description,
            cancellationToken);

        return outcome.Status switch
        {
            // No hay GET por id: publicar un Location apuntaría a la colección.
            CatalogoOutcomeStatus.Saved => Results.Json(
                ToResponse(outcome.Entity!),
                statusCode: StatusCodes.Status201Created),
            _ => Problem(outcome.Status),
        };
    }

    private static async Task<IResult> UpdateEspecialidadAsync(
        Guid id,
        EspecialidadRequest request,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var outcome = await catalogo.SaveEspecialidadAsync(
            id,
            request.Name,
            request.Description,
            cancellationToken);

        return outcome.Status switch
        {
            CatalogoOutcomeStatus.Saved => Results.Ok(ToResponse(outcome.Entity!)),
            _ => Problem(outcome.Status),
        };
    }

    private static async Task<IResult> StatusEspecialidadAsync(
        Guid id,
        CatalogoStatusRequest request,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var outcome = await catalogo.SetEspecialidadAsync(id, request.IsActive, cancellationToken);

        return outcome.Status switch
        {
            CatalogoOutcomeStatus.Saved => Results.Ok(ToResponse(outcome.Entity!)),
            _ => Problem(outcome.Status),
        };
    }

    private static async Task<IResult> CreateServicioAsync(
        ServicioRequest request,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var outcome = await catalogo.SaveServicioAsync(
            null,
            request.Name,
            request.Description,
            request.EspecialidadId,
            cancellationToken);

        return outcome.Status switch
        {
            // No hay GET por id: publicar un Location apuntaría a la colección.
            CatalogoOutcomeStatus.Saved => Results.Json(
                ToResponse(outcome.Entity!),
                statusCode: StatusCodes.Status201Created),
            _ => Problem(outcome.Status),
        };
    }

    private static async Task<IResult> UpdateServicioAsync(
        Guid id,
        ServicioRequest request,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var outcome = await catalogo.SaveServicioAsync(
            id,
            request.Name,
            request.Description,
            request.EspecialidadId,
            cancellationToken);

        return outcome.Status switch
        {
            CatalogoOutcomeStatus.Saved => Results.Ok(ToResponse(outcome.Entity!)),
            _ => Problem(outcome.Status),
        };
    }

    private static async Task<IResult> StatusServicioAsync(
        Guid id,
        CatalogoStatusRequest request,
        CatalogoService catalogo,
        CancellationToken cancellationToken)
    {
        var outcome = await catalogo.SetServicioAsync(id, request.IsActive, cancellationToken);

        return outcome.Status switch
        {
            CatalogoOutcomeStatus.Saved => Results.Ok(ToResponse(outcome.Entity!)),
            _ => Problem(outcome.Status),
        };
    }

    private static EspecialidadResponse ToResponse(Especialidad especialidad) =>
        new(especialidad.Id, especialidad.Name, especialidad.Description, especialidad.IsActive);

    private static ServicioResponse ToResponse(Servicio servicio) =>
        new(servicio.Id, servicio.Name, servicio.Description, servicio.EspecialidadId, servicio.IsActive);

    /// <summary>
    /// Traduce el estado del caso de uso al error HTTP publicado. Un estado nuevo
    /// debe decidir aquí su respuesta en vez de heredar un conflicto por defecto.
    /// </summary>
    private static IResult Problem(CatalogoOutcomeStatus status) => status switch
    {
        CatalogoOutcomeStatus.NotFound => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Registro no encontrado",
            detail: "No existe el registro solicitado."),
        CatalogoOutcomeStatus.DuplicateName => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Nombre duplicado",
            detail: "Ya existe un registro con ese nombre, incluso entre los inactivos."),
        CatalogoOutcomeStatus.ActiveServices => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "La especialidad tiene servicios activos",
            detail: "Desactiva primero los servicios activos."),
        CatalogoOutcomeStatus.InactiveDestination => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "La especialidad de destino está inactiva",
            detail: "Selecciona una especialidad activa."),
        _ => throw new ArgumentOutOfRangeException(
            nameof(status),
            status,
            "Estado del catálogo sin respuesta HTTP."),
    };
}
