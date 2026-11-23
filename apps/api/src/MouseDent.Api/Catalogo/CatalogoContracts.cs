using System.Text.Json.Serialization;

namespace MouseDent.Api.Catalogo;

/// <summary>Datos de alta y edición. La descripción es opcional.</summary>
public sealed record EspecialidadRequest(string Name, string? Description = null);

/// <summary>
/// Datos de alta y edición de un servicio. La descripción es opcional; la
/// especialidad es obligatoria.
/// </summary>
public sealed record ServicioRequest(string Name, Guid EspecialidadId, string? Description = null);

/// <summary>
/// Cambio de estado. El valor es obligatorio: un cuerpo vacío no puede
/// interpretarse como una desactivación.
/// </summary>
public sealed record CatalogoStatusRequest([property: JsonRequired] bool IsActive);

public sealed record EspecialidadResponse(Guid Id, string Name, string? Description, bool IsActive);

public sealed record ServicioResponse(Guid Id, string Name, string? Description, Guid EspecialidadId, bool IsActive);
