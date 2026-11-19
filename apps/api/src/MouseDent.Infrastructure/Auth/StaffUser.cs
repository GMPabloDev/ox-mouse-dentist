using Microsoft.AspNetCore.Identity;
using MouseDent.Domain.Auth;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Cuenta del personal en el almacén de Identity. No sale de Infrastructure.
/// El perfil, el estado y la contraseña establecida son columnas propias: los
/// roles de Identity solo aportan el esquema inicial y no gobiernan el acceso.
/// </summary>
public sealed class StaffUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Perfil único: uno de <see cref="StaffRole.All"/>.</summary>
    public string Profile { get; set; } = StaffRole.Professional;

    /// <summary>Desactivar la cuenta conserva sus datos y bloquea el acceso.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Momento en que la persona estableció su contraseña. Nulo significa cuenta
    /// pendiente: no inicia sesión ni cuenta como administración disponible.
    /// </summary>
    public DateTime? PasswordEstablishedAt { get; set; }

    /// <summary>
    /// Versión de la sesión. Cambia cuando se invalidan los tokens ya emitidos, de
    /// modo que reactivar la cuenta no restaura sesiones anteriores.
    /// </summary>
    public int SessionVersion { get; set; } = 1;

    public ICollection<StaffLocalAssignment> Assignments { get; set; } = [];
}
