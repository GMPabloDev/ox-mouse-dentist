using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Application.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Login_returns_a_token_for_valid_credentials()
    {
        var account = StaffSamples.Account(StaffRole.Admin, "admin@mousedent.local");
        var service = new AuthService(new FakeStaffAccounts { Credentials = account }, new FakeTokenIssuer());

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
        var service = new AuthService(new FakeStaffAccounts(), new FakeTokenIssuer());

        var result = await service.LoginAsync(
            "admin@mousedent.local",
            "clave-incorrecta",
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task A_session_stays_valid_while_the_account_keeps_its_email_profile_and_status()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var service = Service(account);

        var current = await ValidateAsync(service, account, account.SessionVersion);

        current.ShouldBe(account);
    }

    [Fact]
    public async Task A_session_stops_being_valid_when_the_account_is_deactivated()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var version = account.SessionVersion;
        var service = Service(account);

        account.Deactivate();

        (await ValidateAsync(service, account, version)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_does_not_come_back_when_the_account_is_reactivated()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var version = account.SessionVersion;
        var service = Service(account);

        account.Deactivate();
        account.Activate();

        (await ValidateAsync(service, account, version)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_stops_being_valid_when_the_profile_changes()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var version = account.SessionVersion;
        var service = Service(account);

        account.UpdateDetails(account.DisplayName, account.Phone, StaffRole.Professional, []);

        (await ValidateAsync(service, account, version)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_stops_being_valid_when_the_email_changes()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var version = account.SessionVersion;
        var service = Service(account);

        account.ChangeEmail("otra.persona@mousedent.local");

        (await ValidateAsync(service, account, "persona@mousedent.local", version)).ShouldBeNull();
    }

    [Fact]
    public async Task Changing_the_name_or_the_assignments_keeps_the_session()
    {
        var local = Guid.NewGuid();
        var account = StaffSamples.Account(StaffRole.Reception);
        var service = Service(account);

        account.UpdateDetails("Nombre Corregido", "+51 900 000 000", StaffRole.Reception, [local]);

        (await ValidateAsync(service, account, account.SessionVersion)).ShouldBe(account);
    }

    [Fact]
    public async Task A_session_is_rejected_for_a_pending_account()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var service = Service(account);

        (await ValidateAsync(service, account, account.SessionVersion)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_is_rejected_for_an_unknown_account()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var service = new AuthService(new FakeStaffAccounts(), new FakeTokenIssuer());

        var current = await service.ValidateSessionAsync(
            Guid.NewGuid(),
            account.Email,
            account.Profile,
            account.SessionVersion,
            TestContext.Current.CancellationToken);

        current.ShouldBeNull();
    }

    private static AuthService Service(StaffAccount account) =>
        new(new FakeStaffAccounts(account), new FakeTokenIssuer());

    private static Task<StaffAccount?> ValidateAsync(
        AuthService service,
        StaffAccount account,
        int sessionVersion) =>
        ValidateAsync(service, account, account.Email, sessionVersion);

    private static Task<StaffAccount?> ValidateAsync(
        AuthService service,
        StaffAccount account,
        string email,
        int sessionVersion) =>
        service.ValidateSessionAsync(
            account.Id,
            email,
            account.Profile,
            sessionVersion,
            TestContext.Current.CancellationToken);
}
