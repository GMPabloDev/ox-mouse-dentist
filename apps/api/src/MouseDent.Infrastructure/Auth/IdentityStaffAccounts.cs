using Microsoft.AspNetCore.Identity;
using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;

namespace MouseDent.Infrastructure.Auth;

/// <summary>
/// Implementa el puerto <see cref="IStaffAccounts"/> con UserManager de Identity.
/// </summary>
public sealed class IdentityStaffAccounts(UserManager<StaffUser> users) : IStaffAccounts
{
    public async Task<StaffAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || !await users.CheckPasswordAsync(user, password))
        {
            return null;
        }

        return await ToAccountAsync(user);
    }

    public async Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString());
        return user is null ? null : await ToAccountAsync(user);
    }

    private async Task<StaffAccount> ToAccountAsync(StaffUser user)
    {
        var roles = await users.GetRolesAsync(user);

        return new StaffAccount(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.ToArray());
    }
}
