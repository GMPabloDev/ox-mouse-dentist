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

    /// <summary>
    /// Comprueba que la cuenta de un token ya emitido siga vigente: activa, con
    /// contraseña establecida y con el mismo correo y perfil con los que se emitió.
    /// Desactivar la cuenta, cambiar su correo o cambiar su perfil invalida la
    /// sesión en la siguiente solicitud. <paramref name="sessionVersion"/> impide
    /// que una sesión anterior reviva al reactivar la cuenta.
    /// </summary>
    public async Task<StaffAccount?> ValidateSessionAsync(
        Guid accountId,
        string email,
        string profile,
        int sessionVersion,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, cancellationToken);

        if (account is null || !account.IsActive || account.IsPending)
        {
            return null;
        }

        return account.SessionVersion == sessionVersion
            && string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase)
            && string.Equals(account.Profile, profile, StringComparison.Ordinal)
                ? account
                : null;
    }
}
