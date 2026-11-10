using System.ComponentModel.DataAnnotations;

namespace MouseDent.Application.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = "MouseDent";

    [Required]
    public string Audience { get; init; } = "MouseDent";

    /// <summary>
    /// Clave simétrica HMAC. En producción se inyecta con <c>Jwt__SigningKey</c>
    /// y debe tener al menos 32 bytes.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 60 * 24 * 30)]
    public int AccessTokenMinutes { get; init; } = 60;
}
