using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Emite tokens de acceso JWT para el personal. No hay refresh tokens en el MVP.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    public IssuedToken Issue(StaffAccount account)
    {
        var jwt = options.Value;
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AuthClaims.Subject, account.Id.ToString()),
            new(AuthClaims.Name, account.DisplayName),
            new(AuthClaims.Email, account.Email),
            new(AuthClaims.TokenId, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(account.Roles.Select(role => new Claim(AuthClaims.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(Handler.CreateToken(descriptor), expiresAt);
    }
}
