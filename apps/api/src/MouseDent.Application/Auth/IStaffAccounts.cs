using MouseDent.Domain.Auth;

namespace MouseDent.Application.Auth;

/// <summary>
/// Puerto hacia el almacén de cuentas del personal. La implementación vive en
/// Infrastructure y el caso de uso no la conoce.
/// </summary>
public interface IStaffAccounts
{
    Task<StaffAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
