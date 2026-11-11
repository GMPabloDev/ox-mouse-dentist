using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MouseDent.Api.Locales;

/// <summary>Días del horario semanal en orden de lunes a domingo.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LocalWeekDay>))]
public enum LocalWeekDay
{
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
}

/// <summary>
/// Estado de un día del horario. Los siete días se envían y se devuelven
/// siempre: un día cerrado lleva <c>IsClosed = true</c> y las dos horas en
/// <c>null</c>, de modo que la web no tiene que suponer días ausentes.
/// </summary>
public sealed record LocalScheduleDay(
    [property: Description("Día de la semana.")] LocalWeekDay Day,
    [property: Description("Indica si el local no abre ese día.")] bool IsClosed,
    [property: Description("Hora de apertura en formato HH:mm. Null cuando el día está cerrado.")]
    string? OpensAt,
    [property: Description("Hora de cierre en formato HH:mm, posterior a la apertura. Null cuando el día está cerrado.")]
    string? ClosesAt);

/// <summary>Foto del local. La versión cambia con cada archivo y sirve para invalidar caché.</summary>
public sealed record LocalPhotoSummary(
    [property: Description("Tipo de contenido almacenado, por ejemplo image/png.")] string ContentType,
    [property: Description("Versión de la foto: nombre del archivo generado por el servidor.")] string Version);

public sealed record LocalResponse(
    Guid Id,
    string Name,
    string Address,
    string Phone,
    bool IsActive,
    IReadOnlyList<LocalScheduleDay> Schedule,
    LocalPhotoSummary? Photo);

public sealed record LocalStatusRequest(bool IsActive);
