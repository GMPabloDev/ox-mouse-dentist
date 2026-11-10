using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

/// <summary>
/// Caso de uso de acceso del personal. Depende solo de puertos, no de Identity,
/// EF Core ni ASP.NET Core.
/// </summary>
public sealed class AuthService(IStaffAccounts accounts, ITokenIssuer tokens)
{
    public async Task<LoginResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var account = await accounts.ValidateCredentialsAsync(email, password, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var token = tokens.Issue(account);
        return new LoginResult(token.Value, token.ExpiresAt, account);
    }

    public Task<StaffAccount?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        accounts.FindByIdAsync(id, cancellationToken);
}
