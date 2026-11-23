using MouseDent.Application.Auth;
using MouseDent.Application.Catalogo;
using MouseDent.Application.Locales;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Tests;

/// <summary>Almacén de cuentas en memoria para las pruebas de casos de uso.</summary>
internal sealed class FakeStaffAccounts(params StaffAccount[] stored) : IStaffAccounts
{
    private readonly List<StaffAccount> _accounts = [.. stored];

    public List<StaffAccount> Created { get; } = [];

    public List<StaffAccount> Updated { get; } = [];

    /// <summary>Cuenta que acepta <see cref="ValidateCredentialsAsync"/>.</summary>
    public StaffAccount? Credentials { get; init; }

    public StaffSaveResult CreateResult { get; init; } = StaffSaveResult.Saved;

    public StaffSaveResult UpdateResult { get; init; } = StaffSaveResult.Saved;

    public Task<StaffAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken) => Task.FromResult(Credentials);

    public Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.FirstOrDefault(account => account.Id == id));

    public Task<StaffAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.FirstOrDefault(
            account => string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<StaffAccount>> ListAsync(
        string? search,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffAccount>>(_accounts);

    public Task<StaffSaveResult> CreateAsync(StaffAccount account, CancellationToken cancellationToken)
    {
        if (CreateResult == StaffSaveResult.Saved)
        {
            _accounts.Add(account);
            Created.Add(account);
        }

        return Task.FromResult(CreateResult);
    }

    public Task<StaffSaveResult> UpdateAsync(StaffAccount account, StaffAccount expected, CancellationToken cancellationToken)
    {
        if (UpdateResult == StaffSaveResult.Saved)
        {
            Updated.Add(account);
        }

        return Task.FromResult(UpdateResult);
    }

    /// <summary>Fallos de invitación registrados, en orden; el último de una cuenta es su marca.</summary>
    public List<(Guid AccountId, DateTime FailedAt)> InvitationFailures { get; } = [];

    /// <summary>Cuentas cuya marca de fallo se limpió porque un envío se entregó.</summary>
    public List<Guid> ClearedInvitationFailures { get; } = [];

    public Task RecordInvitationFailureAsync(
        Guid accountId,
        DateTime failedAt,
        CancellationToken cancellationToken)
    {
        InvitationFailures.Add((accountId, failedAt));

        return Task.CompletedTask;
    }

    public Task ClearInvitationFailureAsync(Guid accountId, CancellationToken cancellationToken)
    {
        ClearedInvitationFailures.Add(accountId);

        return Task.CompletedTask;
    }
}

/// <summary>Almacén de locales en memoria. No guarda cambios: las pruebas de cuentas solo leen.</summary>
internal sealed class FakeLocalStore(params Local[] stored) : ILocales
{
    private readonly List<Local> _locals = [.. stored];

    public Task<IReadOnlyList<Local>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Local>>(_locals);

    public Task<Local?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_locals.FirstOrDefault(local => local.Id == id));

    public Task<bool> NameIsTakenAsync(
        string normalizedName,
        Guid? excludeLocalId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<LocalSaveResult> SaveAsync(Local local, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>
/// Almacén de enlaces en memoria. Guarda por hash, igual que la base, así que las
/// pruebas comprueban que el token en claro nunca llega al almacén.
/// </summary>
internal sealed class FakeAccessLinks : IStaffAccessLinks
{
    private readonly Dictionary<Guid, StaffAccessLink> _pending = [];

    public List<StaffAccessLink> Stored { get; } = [];

    public List<string> ResetHashes { get; } = [];

    public PasswordResetResult ResetResult { get; init; } = PasswordResetResult.Reset();

    public Task<bool> ReplacePendingAsync(StaffAccessLink link, StaffAccount expected, bool requirePending, CancellationToken cancellationToken)
    {
        _pending[link.AccountId] = link;
        Stored.Add(link);

        return Task.FromResult(true);
    }

    /// <summary>El almacén solo reconoce un hash de un enlace pendiente, nunca un token en claro.</summary>
    public Task<PasswordResetResult> ResetPasswordAsync(
        string tokenHash,
        string password,
        CancellationToken cancellationToken)
    {
        ResetHashes.Add(tokenHash);

        var known = _pending.Values.Any(link => link.TokenHash == tokenHash);

        return Task.FromResult(known ? ResetResult : PasswordResetResult.LinkUnusable());
    }

}

/// <summary>Envío de correo en memoria. Puede simular un fallo del servidor SMTP.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Cuando es <c>true</c>, el envío falla como un servidor SMTP caído.</summary>
    public bool Fails { get; init; }

    /// <summary>
    /// Fallo que no es del protocolo SMTP, como un adaptador mal configurado. El
    /// envío no llega a salir y la excepción no es de entrega.
    /// </summary>
    public Func<Exception>? Throws { get; init; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Throws is { } failure)
        {
            throw failure();
        }

        if (Fails)
        {
            throw new EmailDeliveryException("El servidor SMTP de prueba rechazó el envío.");
        }

        Sent.Add(message);

        return Task.CompletedTask;
    }
}

/// <summary>Reloj fijo para que los casos de uso no dependan del reloj del sistema.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeTokenIssuer : ITokenIssuer
{
    public IssuedToken Issue(StaffAccount account) => new("token-de-prueba", DateTimeOffset.UnixEpoch);
}

/// <summary>Cuentas, locales y servicios con los que se construyen los casos de prueba.</summary>
internal static class StaffSamples
{
    public static readonly DateTime EstablishedAt = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    public const string PublicBaseUrl = "https://panel.mousedent.test";

    public static StaffAccount Account(
        string profile,
        string email = "persona@mousedent.local",
        params Guid[] assignedLocalIds)
    {
        var account = StaffAccount.Invite("Persona de prueba", email, "+51 999 888 777", profile, assignedLocalIds);
        account.RegisterPasswordEstablished(EstablishedAt);

        return account;
    }

    public static StaffAccount Pending(string profile, string email = "pendiente@mousedent.local") =>
        StaffAccount.Invite("Persona pendiente", email, null, profile, []);

    public static Local NewLocal(string name, bool isActive = true)
    {
        var local = Local.Create(
            name,
            "Av. Siempre Viva 123",
            "+51 999 888 777",
            Schedules.WeekFromNineToEighteen());

        if (!isActive)
        {
            local.Deactivate();
        }

        return local;
    }

    public static StaffAccessOptions AccessOptions() => new() { PublicBaseUrl = PublicBaseUrl };

    public static StaffAccessService AccessService(
        IStaffAccounts accounts,
        IStaffAccessLinks links,
        IEmailSender email,
        DateTimeOffset? now = null) =>
        new(accounts, links, email, AccessOptions(), new FixedClock(now ?? EstablishedAt));

    public static StaffManagementService ManagementService(
        FakeStaffAccounts accounts,
        FakeLocalStore locales,
        FakeAccessLinks? links = null,
        FakeEmailSender? email = null) =>
        new(accounts, locales, AccessService(accounts, links ?? new FakeAccessLinks(), email ?? new FakeEmailSender()));
}
