using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;
using Shouldly;

namespace MouseDent.Application.Tests;

public sealed class StaffManagementServiceTests
{
    [Fact]
    public async Task Creating_a_reception_account_saves_it_pending_with_its_active_locals()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var accounts = new FakeStaffAccounts();
        var service = Service(accounts, new FakeLocalStore(local));

        var outcome = await service.CreateAsync(
            Details(StaffRole.Reception, "nueva@mousedent.local", [local.Id]),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        var created = outcome.Account.ShouldNotBeNull();
        created.IsPending.ShouldBeTrue();
        created.IsActive.ShouldBeTrue();
        created.AssignedLocalIds.ShouldBe([local.Id]);
        accounts.Created.ShouldBe([created]);
    }

    [Fact]
    public async Task Creating_a_reception_account_without_locals_is_rejected()
    {
        var accounts = new FakeStaffAccounts();
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.CreateAsync(
            Details(StaffRole.Reception, "nueva@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.ReceptionRequiresLocal);
        accounts.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_a_reception_account_with_an_inactive_local_is_rejected()
    {
        var inactive = StaffSamples.NewLocal("Sede Cerrada", isActive: false);
        var accounts = new FakeStaffAccounts();
        var service = Service(accounts, new FakeLocalStore(inactive));

        var outcome = await service.CreateAsync(
            Details(StaffRole.Reception, "nueva@mousedent.local", [inactive.Id]),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.LocalsUnavailable);
        accounts.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_a_reception_account_with_an_unknown_local_is_rejected()
    {
        var accounts = new FakeStaffAccounts();
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.CreateAsync(
            Details(StaffRole.Reception, "nueva@mousedent.local", [Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.LocalsUnavailable);
        accounts.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_an_administrator_with_locals_is_rejected()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var accounts = new FakeStaffAccounts();
        var service = Service(accounts, new FakeLocalStore(local));

        var outcome = await service.CreateAsync(
            Details(StaffRole.Admin, "nueva@mousedent.local", [local.Id]),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.LocalsNotAllowed);
        accounts.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_a_professional_without_local_assignments_is_allowed()
    {
        var accounts = new FakeStaffAccounts();
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.CreateAsync(
            Details(StaffRole.Professional, "profesional@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.AssignedLocalIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_an_account_with_an_email_already_in_use_reports_the_conflict()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var accounts = new FakeStaffAccounts { CreateResult = StaffSaveResult.DuplicateEmail };
        var service = Service(accounts, new FakeLocalStore(local));

        var outcome = await service.CreateAsync(
            Details(StaffRole.Reception, "repetida@mousedent.local", [local.Id]),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.DuplicateEmail);
    }

    [Fact]
    public async Task A_new_assignment_to_an_inactive_local_is_rejected_on_update()
    {
        var assigned = StaffSamples.NewLocal("Sede Centro");
        var inactive = StaffSamples.NewLocal("Sede Cerrada", isActive: false);
        var account = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [assigned.Id]);
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore(assigned, inactive));

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Reception, account.Email, [assigned.Id, inactive.Id]),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.LocalsUnavailable);
        accounts.Updated.ShouldBeEmpty();
        account.AssignedLocalIds.ShouldBe([assigned.Id]);
    }

    [Fact]
    public async Task An_existing_assignment_to_an_inactive_local_is_kept_while_editing_other_data()
    {
        var inactive = StaffSamples.NewLocal("Sede Cerrada", isActive: false);
        var account = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [inactive.Id]);
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore(inactive));

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Reception, account.Email, [inactive.Id], displayName: "Nombre Corregido"),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.DisplayName.ShouldBe("Nombre Corregido");
        outcome.Account.AssignedLocalIds.ShouldBe([inactive.Id]);
        accounts.Updated.ShouldBe([account]);
    }

    [Fact]
    public async Task A_reception_account_that_loses_every_assignment_keeps_a_valid_state()
    {
        var inactive = StaffSamples.NewLocal("Sede Cerrada", isActive: false);
        var account = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [inactive.Id]);
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore(inactive));

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Reception, account.Email, []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.AssignedLocalIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_the_profile_to_professional_drops_the_reception_assignments()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [local.Id]);
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore(local));

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Professional, account.Email, []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.Profile.ShouldBe(StaffRole.Professional);
        outcome.Account.AssignedLocalIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_the_email_leaves_the_account_pending()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Reception, "correo.nuevo@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.Email.ShouldBe("correo.nuevo@mousedent.local");
        outcome.Account.IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task Rewriting_the_same_email_keeps_the_established_password()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "persona@mousedent.local");
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Reception, "PERSONA@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.IsPending.ShouldBeFalse();
    }

    [Fact]
    public async Task The_use_case_reports_the_last_administrator_protection_from_the_store()
    {
        var admin = StaffSamples.Account(StaffRole.Admin);
        var accounts = new FakeStaffAccounts(admin) { UpdateResult = StaffSaveResult.LastAdministrator };
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.SetStatusAsync(admin.Id, false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.LastAdministrator);
        accounts.Updated.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reactivating_a_credentialed_account_makes_it_active_again()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        account.Deactivate();
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.SetStatusAsync(account.Id, true, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.IsActive.ShouldBeTrue();
        outcome.Account.IsPending.ShouldBeFalse();
        accounts.Updated.ShouldBe([account]);
    }

    [Fact]
    public async Task Reactivating_a_pending_account_leaves_it_pending()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        account.Deactivate();
        var accounts = new FakeStaffAccounts(account);
        var service = Service(accounts, new FakeLocalStore());

        var outcome = await service.SetStatusAsync(account.Id, true, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.IsActive.ShouldBeTrue();
        outcome.Account.IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task Updating_an_unknown_account_reports_not_found()
    {
        var service = Service(new FakeStaffAccounts(), new FakeLocalStore());

        var outcome = await service.UpdateAsync(
            Guid.NewGuid(),
            Details(StaffRole.Professional, "persona@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.NotFound);
    }

    [Fact]
    public async Task Creating_an_account_sends_the_invitation_to_its_email()
    {
        var accounts = new FakeStaffAccounts();
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();
        var service = Service(accounts, new FakeLocalStore(), links, email);

        var outcome = await service.CreateAsync(
            Details(StaffRole.Professional, "nueva@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        var sent = email.Sent.ShouldHaveSingleItem();
        sent.To.ShouldBe("nueva@mousedent.local");
        sent.Subject.ShouldContain("MouseDent");
        sent.Body.ShouldContain($"{StaffSamples.PublicBaseUrl}/establecer-contrasena?token=");
        links.Stored.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_failed_invitation_keeps_the_account_pending_and_reports_the_failure()
    {
        var accounts = new FakeStaffAccounts();
        var service = Service(accounts, new FakeLocalStore(), email: new FakeEmailSender { Fails = true });

        var outcome = await service.CreateAsync(
            Details(StaffRole.Professional, "nueva@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.InvitationFailed);
        outcome.Account.ShouldNotBeNull();
        outcome.Account.IsPending.ShouldBeTrue();
        accounts.Created.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Resending_an_invitation_replaces_the_previous_link()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        var accounts = new FakeStaffAccounts(account);
        var links = new FakeAccessLinks();
        var email = new FakeEmailSender();
        var service = Service(accounts, new FakeLocalStore(), links, email);

        (await service.ResendInvitationAsync(account.Id, TestContext.Current.CancellationToken))
            .Status.ShouldBe(StaffOutcomeStatus.Saved);

        var sent = email.Sent.ShouldHaveSingleItem();
        var token = LinkToken(sent);

        (await StaffSamples
                .AccessService(accounts, links, email)
                .ResetPasswordAsync(token, "Recepcion123!", TestContext.Current.CancellationToken))
            .Status.ShouldBe(PasswordResetStatus.Reset);
        links.Stored.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Resending_to_an_inactive_account_is_rejected_without_creating_links()
    {
        var account = StaffSamples.Pending(StaffRole.Reception);
        account.Deactivate();
        var links = new FakeAccessLinks();
        var service = Service(new FakeStaffAccounts(account), new FakeLocalStore(), links);

        var outcome = await service.ResendInvitationAsync(account.Id, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.AccountInactive);
        links.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resending_to_an_account_with_password_is_rejected()
    {
        var account = StaffSamples.Account(StaffRole.Reception);
        var email = new FakeEmailSender();
        var service = Service(new FakeStaffAccounts(account), new FakeLocalStore(), email: email);

        var outcome = await service.ResendInvitationAsync(account.Id, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.PasswordAlreadyEstablished);
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_the_email_invites_the_new_address_and_leaves_the_account_pending()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "anterior@mousedent.local");
        var email = new FakeEmailSender();
        var links = new FakeAccessLinks();
        var service = Service(new FakeStaffAccounts(account), new FakeLocalStore(), links, email);

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Reception, "nuevo@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.Saved);
        outcome.Account!.IsPending.ShouldBeTrue();
        email.Sent.ShouldHaveSingleItem().To.ShouldBe("nuevo@mousedent.local");
        links.Stored.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_failed_invitation_after_an_email_change_keeps_the_account_pending()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "anterior@mousedent.local");
        var service = Service(
            new FakeStaffAccounts(account),
            new FakeLocalStore(),
            email: new FakeEmailSender { Fails = true });

        var outcome = await service.UpdateAsync(
            account.Id,
            Details(StaffRole.Reception, "nuevo@mousedent.local", []),
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(StaffOutcomeStatus.InvitationFailed);
        outcome.Account!.Email.ShouldBe("nuevo@mousedent.local");
        outcome.Account.IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task Rewriting_the_same_email_does_not_send_another_invitation()
    {
        var account = StaffSamples.Account(StaffRole.Reception, "persona@mousedent.local");
        var email = new FakeEmailSender();
        var service = Service(new FakeStaffAccounts(account), new FakeLocalStore(), email: email);

        (await service.UpdateAsync(
                account.Id,
                Details(StaffRole.Reception, "PERSONA@mousedent.local", []),
                TestContext.Current.CancellationToken))
            .Status.ShouldBe(StaffOutcomeStatus.Saved);

        email.Sent.ShouldBeEmpty();
    }

    private static string LinkToken(EmailMessage message)
    {
        var start = message.Body.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        var rest = message.Body[start..];
        var end = rest.IndexOfAny(['\r', '\n']);

        return end < 0 ? rest : rest[..end];
    }

    private static StaffManagementService Service(
        FakeStaffAccounts accounts,
        FakeLocalStore locales,
        FakeAccessLinks? links = null,
        FakeEmailSender? email = null) =>
        StaffSamples.ManagementService(accounts, locales, links, email);

    private static StaffAccountDetails Details(
        string profile,
        string email,
        IReadOnlyList<Guid> assignedLocalIds,
        string displayName = "Persona de prueba") =>
        new(displayName, email, "+51 999 888 777", profile, assignedLocalIds);
}
