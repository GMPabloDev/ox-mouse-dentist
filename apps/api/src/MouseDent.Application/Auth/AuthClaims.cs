namespace MouseDent.Application.Auth;

/// <summary>
/// Nombres cortos de los claims. Coinciden con <c>TokenValidationParameters</c>
/// porque el bearer desactiva el mapeo inbound.
/// </summary>
public static class AuthClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string TokenId = "jti";
}
