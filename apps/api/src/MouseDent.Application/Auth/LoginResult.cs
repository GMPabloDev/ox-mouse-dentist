using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    StaffAccount Account);
