using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

/// <summary>
/// Puerto de emisión de tokens. La implementación JWT vive en Infrastructure.
/// </summary>
public interface ITokenIssuer
{
    IssuedToken Issue(StaffAccount account);
}

public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAt);
