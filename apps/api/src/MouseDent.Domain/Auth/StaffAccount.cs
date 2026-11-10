namespace MouseDent.Domain.Auth;

/// <summary>
/// Cuenta del personal dentro del dominio. No conoce Identity ni la persistencia.
/// </summary>
public sealed record StaffAccount(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
