using System.ComponentModel;

namespace MouseDent.Api.Auth;

/// <summary>
/// Datos editables de una cuenta del personal. Un perfil por cuenta: recepción
/// se asigna a locales; administración accede a todos por su perfil y un
/// profesional puede existir sin asignaciones.
/// </summary>
public class StaffAccountForm
{
    [Description("Nombre visible de la persona.")]
    public string DisplayName { get; set; } = string.Empty;

    [Description("Correo que identifica la cuenta. Único entre todas, incluidas las inactivas y pendientes.")]
    public string Email { get; set; } = string.Empty;

    [Description("Teléfono opcional. No se exige un formato concreto.")]
    public string? Phone { get; set; }

    [Description("Perfil único: Administrador, Recepcion o Profesional.")]
    public string Profile { get; set; } = string.Empty;

    [Description("Locales asignados. Solo recepción los recibe, y al añadir uno nuevo debe estar activo.")]
    public IReadOnlyList<Guid>? AssignedLocalIds { get; set; }
}

public sealed class CreateStaffAccountForm : StaffAccountForm;

public sealed class UpdateStaffAccountForm : StaffAccountForm;
