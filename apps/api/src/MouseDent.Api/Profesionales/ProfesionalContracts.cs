using System.ComponentModel;
using System.Text.Json.Serialization;
using MouseDent.Api.Auth;

namespace MouseDent.Api.Profesionales;

/// <summary>
/// Ficha del profesional con los datos de su cuenta. Es la misma forma para la
/// lista y para la ficha: el nombre, el correo y el teléfono se leen de la cuenta,
/// y las especialidades y los locales se muestran con el estado que hoy tienen en
/// el catálogo y en el local. El estado de la ficha y el de la cuenta son
/// independientes.
/// </summary>
public sealed record ProfesionalResponse(
    [property: Description("Identificador de la ficha.")] Guid Id,
    [property: Description("Identificador de la cuenta vinculada.")] Guid StaffAccountId,
    [property: Description("Nombre visible de la cuenta.")] string DisplayName,
    [property: Description("Correo de la cuenta. Único entre todas, incluidas las inactivas.")] string Email,
    [property: Description("Teléfono de la cuenta, opcional.")] string? Phone,
    [property: Description("Número de colegiatura de la ficha, opcional.")] string? Colegiatura,
    [property: Description("Estado de la ficha: true activa, false inactiva. Es independiente del estado de la cuenta.")] bool IsActive,
    [property: Description("Estado de la cuenta: Pendiente, Activa o Inactiva.")] StaffAccountState AccountStatus,
    [property: Description("Indica que el último envío de la invitación falló y la cuenta sigue pendiente.")] bool InvitationFailed,
    [property: Description("Especialidades asignadas, con su estado actual en el catálogo.")] IReadOnlyList<ProfesionalEspecialidadResponse> Especialidades,
    [property: Description("Locales de atención asignados, con su estado actual.")] IReadOnlyList<ProfesionalLocalResponse> Locales);

/// <summary>Especialidad asignada a la ficha, con su estado actual en el catálogo.</summary>
public sealed record ProfesionalEspecialidadResponse(
    [property: Description("Identificador de la especialidad.")] Guid Id,
    [property: Description("Nombre de la especialidad.")] string Name,
    [property: Description("Indica si la especialidad sigue activa. Una asignación inactiva se conserva y se muestra marcada.")] bool IsActive);

/// <summary>Local de atención asignado a la ficha, con su estado actual.</summary>
public sealed record ProfesionalLocalResponse(
    [property: Description("Identificador del local.")] Guid Id,
    [property: Description("Nombre del local.")] string Name,
    [property: Description("Indica si el local sigue activo. Una asignación inactiva se conserva y se muestra marcada.")] bool IsActive);

/// <summary>
/// Estado que se fija a la ficha sin editar el resto de sus datos. El valor es
/// obligatorio: un cuerpo sin <c>IsActive</c> no puede interpretarse como una
/// desactivación.
/// </summary>
public sealed record ProfesionalStatusRequest(
    [property: JsonRequired]
    [property: Description("true reactiva la ficha; false la desactiva conservando sus especialidades, sus locales y su cuenta.")]
    bool IsActive);
