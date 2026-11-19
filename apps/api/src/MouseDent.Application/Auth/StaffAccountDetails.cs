namespace MouseDent.Application.Auth;

/// <summary>Datos editables de una cuenta del personal.</summary>
public sealed record StaffAccountDetails(
    string DisplayName,
    string Email,
    string? Phone,
    string Profile,
    IReadOnlyList<Guid> AssignedLocalIds);
