namespace MouseDent.Api.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    AuthenticatedUser User);

/// <summary>Solicitud pública de un enlace de recuperación.</summary>
public sealed record PasswordRecoveryRequest(string Email);

/// <summary>Establece la contraseña con el token de un enlace de invitación o recuperación.</summary>
public sealed record ResetPasswordRequest(string Token, string Password);
