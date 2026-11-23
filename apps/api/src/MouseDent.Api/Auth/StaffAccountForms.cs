using System.ComponentModel;

namespace MouseDent.Api.Auth;

/// <summary>
/// Datos editables de una cuenta del personal. Un perfil por cuenta: recepción
/// se asigna a locales; administración accede a todos por su perfil y un
/// profesional puede existir sin asignaciones.
/// </summary>
public abstract class StaffAccountForm
{
    [Description("Nombre visible de la persona.")]
    public string DisplayName { get; set; } = string.Empty;

    [Description("Correo que identifica la cuenta. Único entre todas, incluidas las inactivas y pendientes.")]
    public string Email { get; set; } = string.Empty;

    [Description("Teléfono opcional. No se exige un formato concreto.")]
    public string? Phone { get; set; }

    /// <summary>
    /// Perfil único de la cuenta. Cada formulario publica su propia descripción:
    /// el alta no asigna el perfil Profesional y la edición solo lo conserva.
    /// </summary>
    public abstract string Profile { get; set; }

    [Description("Locales asignados. Solo recepción los recibe, y al añadir uno nuevo debe estar activo.")]
    public IReadOnlyList<Guid>? AssignedLocalIds { get; set; }
}

/// <summary>Alta de una cuenta desde personal, que solo crea administración y recepción.</summary>
public sealed class CreateStaffAccountForm : StaffAccountForm
{
    [Description("Perfil único: Administrador o Recepcion. El perfil Profesional se asigna en el alta de profesionales, junto con la ficha.")]
    public override string Profile { get; set; } = string.Empty;
}

/// <summary>Edición de una cuenta existente, que conserva el perfil Profesional si lo tiene.</summary>
public sealed class UpdateStaffAccountForm : StaffAccountForm
{
    [Description("Perfil único: Administrador o Recepcion. El perfil Profesional de una cuenta existente se conserva y se asigna en el alta de profesionales.")]
    public override string Profile { get; set; } = string.Empty;
}
