using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Application.Tests;

/// <summary>
/// Caso de uso de los enlaces de acceso. La caducidad, el uso único y la
/// invalidación en la base se comprueban contra el almacén real en las pruebas
/// de la API; aquí se fijan las decisiones del caso de uso.
/// </summary>
public sealed class StaffAccessServiceTests
{
    [Fact]
    public async Task An_invitation_goes_to_the_account_address_with_a_link_to_the_public_screen()
    {
        var account = StaffSamples.Pending(StaffRole.Reception, "invitada@mousedent.local");
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();

        var sent = await Service(new FakeStaffAccounts(account), links, email)
            .SendInvitationAsync(account, TestContext.Current.CancellationToken);

        sent.ShouldBeTrue();
        var message = email.Sent.ShouldHaveSingleItem();
        message.To.ShouldBe("invitada@mousedent.local");
        message.Subject.ShouldContain("MouseDent");
        message.Subject.ShouldContain("invitación");
        message.Body.ShouldContain($"{StaffSamples.PublicBaseUrl}/establecer-contrasena?token=");
        message.Body.ShouldContain(account.DisplayName);
        links.Stored.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_store_never_sees_the_token_in_clear_text()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();

        await Service(new FakeStaffAccounts(account), links, email)
            .SendInvitationAsync(account, TestContext.Current.CancellationToken);

        var token = TokenOf(email.Sent.ShouldHaveSingleItem());
        var stored = links.Stored.ShouldHaveSingleItem();

        token.ShouldNotBeNullOrWhiteSpace();
        stored.TokenHash.ShouldNotBe(token);
        stored.AccountId.ShouldBe(account.Id);
    }

    [Fact]
    public async Task A_delivery_failure_is_reported_without_an_email()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var email = new FakeEmailSender { Fails = true };
        var links = new FakeAccessLinks();

        var sent = await Service(new FakeStaffAccounts(account), links, email)
            .SendInvitationAsync(account, TestContext.Current.CancellationToken);

        sent.ShouldBeFalse();
        email.Sent.ShouldBeEmpty();

        // El enlace se emitió antes del envío, así que el anterior ya no sirve.
        links.Stored.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task An_invitation_that_does_not_leave_is_marked_on_the_account()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var accounts = new FakeStaffAccounts(account);

        await Service(accounts, new FakeAccessLinks(), new FakeEmailSender { Fails = true })
            .SendInvitationAsync(account, TestContext.Current.CancellationToken);

        var (accountId, failedAt) = accounts.InvitationFailures.ShouldHaveSingleItem();
        accountId.ShouldBe(account.Id);
        failedAt.ShouldBe(StaffSamples.EstablishedAt);
        accounts.ClearedInvitationFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_delivered_invitation_clears_the_previous_failure_mark()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var accounts = new FakeStaffAccounts(account);

        var sent = await Service(accounts, new FakeAccessLinks(), new FakeEmailSender())
            .SendInvitationAsync(account, TestContext.Current.CancellationToken);

        sent.ShouldBeTrue();
        accounts.ClearedInvitationFailures.ShouldBe([account.Id]);
        accounts.InvitationFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_send_that_fails_for_another_reason_counts_as_an_undelivered_invitation()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var accounts = new FakeStaffAccounts(account);
        var email = new FakeEmailSender
        {
            Throws = () => new InvalidOperationException("El adaptador de correo no está configurado."),
        };

        var sent = await Service(accounts, new FakeAccessLinks(), email)
            .SendInvitationAsync(account, TestContext.Current.CancellationToken);

        sent.ShouldBeFalse();
        accounts.InvitationFailures.ShouldHaveSingleItem().AccountId.ShouldBe(account.Id);
        accounts.ClearedInvitationFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_cancelled_send_propagates_and_does_not_record_a_failure()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var accounts = new FakeStaffAccounts(account);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            Service(accounts, new FakeAccessLinks(), new FakeEmailSender())
                .SendInvitationAsync(account, cancellation.Token));

        accounts.InvitationFailures.ShouldBeEmpty();
        accounts.ClearedInvitationFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_recovery_request_sends_a_link_to_an_active_account()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "persona@mousedent.local");
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();

        var status = await Service(new FakeStaffAccounts(account), links, email)
            .RequestRecoveryAsync("persona@mousedent.local", TestContext.Current.CancellationToken);

        status.ShouldBe(PasswordRecoveryStatus.Sent);

        var message = email.Sent.ShouldHaveSingleItem();
        message.To.ShouldBe("persona@mousedent.local");
        message.Subject.ShouldContain("Recupera");
        message.Body.ShouldContain($"{StaffSamples.PublicBaseUrl}/establecer-contrasena?token=");

        links.Stored.ShouldHaveSingleItem().AccountId.ShouldBe(account.Id);
    }

    [Fact]
    public async Task A_recovery_request_for_an_unknown_address_sends_nothing()
    {
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();

        var status = await Service(new FakeStaffAccounts(), links, email)
            .RequestRecoveryAsync("nadie@mousedent.local", TestContext.Current.CancellationToken);

        status.ShouldBe(PasswordRecoveryStatus.Ignored);
        email.Sent.ShouldBeEmpty();
        links.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_recovery_request_for_an_inactive_account_does_not_produce_a_usable_link()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "inactiva@mousedent.local");
        account.Deactivate();
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();

        var status = await Service(new FakeStaffAccounts(account), links, email)
            .RequestRecoveryAsync("inactiva@mousedent.local", TestContext.Current.CancellationToken);

        status.ShouldBe(PasswordRecoveryStatus.Ignored);

        email.Sent.ShouldBeEmpty();
        links.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task Requesting_recovery_does_not_touch_the_sessions()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "persona@mousedent.local");
        var version = account.SessionVersion;

        await Service(new FakeStaffAccounts(account), new FakeAccessLinks(), new FakeEmailSender())
            .RequestRecoveryAsync("persona@mousedent.local", TestContext.Current.CancellationToken);

        account.SessionVersion.ShouldBe(version);
    }

    [Fact]
    public async Task Completing_the_link_hands_the_store_a_hash_of_the_token()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();
        var access = Service(new FakeStaffAccounts(account), links, email);

        await access.SendInvitationAsync(account, TestContext.Current.CancellationToken);
        var token = TokenOf(email.Sent.ShouldHaveSingleItem());

        var result = await access.ResetPasswordAsync(
            $"  {token}  ",
            "Recepcion123!",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(PasswordResetStatus.Reset);
        links.ResetHashes.ShouldHaveSingleItem().ShouldNotBe(token);
    }

    [Fact]
    public async Task An_unknown_token_is_rejected_by_the_store()
    {
        var links = new FakeAccessLinks();
        var access = Service(new FakeStaffAccounts(), links, new FakeEmailSender());

        var result = await access.ResetPasswordAsync(
            "token-que-nadie-emitio",
            "Recepcion123!",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(PasswordResetStatus.LinkUnusable);
    }

    [Fact]
    public async Task A_rejected_password_policy_is_reported_to_the_caller()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks
        {
            ResetResult = PasswordResetResult.PasswordRejected(["PasswordRequiresNonAlphanumeric"]),
        };
        var access = Service(new FakeStaffAccounts(account), links, email);

        await access.SendInvitationAsync(account, TestContext.Current.CancellationToken);
        var token = TokenOf(email.Sent.ShouldHaveSingleItem());

        var result = await access.ResetPasswordAsync(
            token,
            "Recepcion123",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(PasswordResetStatus.PasswordRejected);
        result.PasswordErrors.ShouldBe(["PasswordRequiresNonAlphanumeric"]);
    }

    [Fact]
    public async Task Recovery_with_missing_public_url_does_not_throw_for_an_existing_account()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "persona@mousedent.local");
        var access = new StaffAccessService(
            new FakeStaffAccounts(account),
            new FakeAccessLinks(),
            new FakeEmailSender(),
            new StaffAccessOptions(),
            TimeProvider.System);

        var status = await access.RequestRecoveryAsync(account.Email, TestContext.Current.CancellationToken);

        status.ShouldBe(PasswordRecoveryStatus.Failed);
    }

    [Fact]
    public async Task Without_a_public_url_the_link_cannot_be_built()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var access = new StaffAccessService(
            new FakeStaffAccounts(account),
            new FakeAccessLinks(),
            new FakeEmailSender(),
            new StaffAccessOptions(),
            TimeProvider.System);

        var sent = await access.SendInvitationAsync(account, TestContext.Current.CancellationToken);
        sent.ShouldBeFalse();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("relative")]
    [InlineData("ftp://example.com")]
    public async Task Invalid_public_url_rejects_invitations_and_all_recovery_requests(string url)
    {
        var account = StaffSamples.Pending(StaffRole.Reception, "persona@mousedent.local");
        var links = new FakeAccessLinks();
        var email = new FakeEmailSender();
        var access = new StaffAccessService(new FakeStaffAccounts(account), links, email,
            new StaffAccessOptions { PublicBaseUrl = url }, TimeProvider.System);
        (await access.SendInvitationAsync(account, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await access.RequestRecoveryAsync(account.Email, TestContext.Current.CancellationToken)).ShouldBe(PasswordRecoveryStatus.Failed);
        (await access.RequestRecoveryAsync("missing@example.com", TestContext.Current.CancellationToken)).ShouldBe(PasswordRecoveryStatus.Failed);
        links.Stored.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    private static StaffAccessService Service(
        FakeStaffAccounts accounts,
        FakeAccessLinks links,
        FakeEmailSender email) =>
        StaffSamples.AccessService(accounts, links, email);

    private static string TokenOf(EmailMessage message)
    {
        const string Marker = "token=";
        var start = message.Body.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
        var rest = message.Body[start..];
        var end = rest.IndexOfAny(['\r', '\n']);

        return end < 0 ? rest : rest[..end];
    }
}
