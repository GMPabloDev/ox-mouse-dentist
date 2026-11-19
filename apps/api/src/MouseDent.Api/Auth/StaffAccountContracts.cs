using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MouseDent.Api.Auth;

/// <summary>
/// Estado visible de una cuenta. Pendiente significa que la persona todavía no
/// estableció su contraseña; inactiva, que se le retiró el acceso sin borrar sus
/// datos.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StaffAccountState>))]
public enum StaffAccountState
{
    Pendiente,
    Activa,
    Inactiva,
}

public sealed record StaffAccountResponse(
    [property: Description("Identificador de la cuenta.")] Guid Id,
    [property: Description("Nombre visible.")] string DisplayName,
    [property: Description("Correo que identifica la cuenta. Único entre todas, incluidas las inactivas.")] string Email,
    [property: Description("Teléfono opcional.")] string? Phone,
    [property: Description("Perfil único: Administrador, Recepcion o Profesional.")] string Profile,
    [property: Description("Pendiente, Activa o Inactiva.")] StaffAccountState Status,
    [property: Description("Locales asignados. Solo recepción recibe asignaciones.")] IReadOnlyList<Guid> AssignedLocalIds);

/// <summary>Estado que se fija a una cuenta sin editar el resto de sus datos.</summary>
public sealed record StaffStatusRequest(
    [property: Description("true reactiva la cuenta; false la desactiva sin borrar sus datos.")] bool IsActive);
