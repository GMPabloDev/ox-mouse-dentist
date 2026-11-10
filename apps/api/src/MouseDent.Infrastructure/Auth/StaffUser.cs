using Microsoft.AspNetCore.Identity;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Cuenta del personal en el almacén de Identity. No sale de Infrastructure.
/// </summary>
public sealed class StaffUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
