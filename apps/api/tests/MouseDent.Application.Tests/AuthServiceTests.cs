using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Application.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Login_returns_a_token_for_valid_credentials()
    {
        var account = new StaffAccount(
            Guid.NewGuid(),
            "admin@mousedent.local",
            "Administración MouseDent",
            [StaffRole.Admin]);

        var service = new AuthService(new FakeStaffAccounts(account), new FakeTokenIssuer());

        var result = await service.LoginAsync(
            "admin@mousedent.local",
            "Admin123!",
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("token-de-prueba");
        result.Account.ShouldBe(account);
    }

    [Fact]
    public async Task Login_returns_null_for_invalid_credentials()
    {
        var service = new AuthService(new FakeStaffAccounts(null), new FakeTokenIssuer());

        var result = await service.LoginAsync(
            "admin@mousedent.local",
            "clave-incorrecta",
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    private sealed class FakeStaffAccounts(StaffAccount? account) : IStaffAccounts
    {
        public Task<StaffAccount?> ValidateCredentialsAsync(
            string email,
            string password,
            CancellationToken cancellationToken) => Task.FromResult(account);

        public Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(account);
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public IssuedToken Issue(StaffAccount account) =>
            new("token-de-prueba", DateTimeOffset.UnixEpoch);
    }
}
