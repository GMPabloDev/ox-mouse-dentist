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
