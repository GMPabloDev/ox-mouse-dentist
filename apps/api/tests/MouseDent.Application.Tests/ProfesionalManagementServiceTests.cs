using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Profesionales;
using Shouldly;

namespace MouseDent.Application.Tests;

/// <summary>
/// Decisiones del alta y del reenvío de la invitación. Las pruebas comprueban qué
/// se guarda, qué se envía y qué se rechaza antes de escribir, sin depender de la
/// base: la atomicidad de la escritura se comprueba en las pruebas de integración.
/// </summary>
public sealed class ProfesionalManagementServiceTests
{
    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_email_creates_a_pending_professional_account_with_its_ficha_and_invitation()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore();
        var accounts = new FakeStaffAccounts();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            accounts,
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("nueva@mousedent.test", [especialidad.Id], [local.Id], " COP 12345 "),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        var (account, ficha) = altas.Created.ShouldHaveSingleItem();

        account.Id.ShouldBe(ficha.StaffAccountId);
        account.Profile.ShouldBe(StaffRole.Professional);
        account.IsActive.ShouldBeTrue();
        account.IsPending.ShouldBeTrue();
        account.Phone.ShouldBeNull();
        account.AssignedLocalIds.ShouldBeEmpty();
        account.Email.ShouldBe("nueva@mousedent.test");
        account.DisplayName.ShouldBe("Persona de alta");

        ficha.IsActive.ShouldBeTrue();
        ficha.Colegiatura.ShouldBe("COP 12345");
        ficha.EspecialidadIds.ShouldBe([especialidad.Id]);
        ficha.LocalIds.ShouldBe([local.Id]);

        email.Sent.ShouldHaveSingleItem().To.ShouldBe("nueva@mousedent.test");
        accounts.ClearedInvitationFailures.ShouldBe([account.Id]);
        accounts.InvitationFailures.ShouldBeEmpty();
        altas.Linked.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_invitation_keeps_the_ficha_and_the_account_and_records_the_failure()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore();
        var accounts = new FakeStaffAccounts();
        var service = ProfesionalSamples.Service(
            altas,
            accounts,
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: new FakeEmailSender { Fails = true });

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("fallida@mousedent.test", [especialidad.Id], [local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.InvitationFailed);
        outcome.Ficha.ShouldNotBeNull();
        outcome.Account.ShouldNotBeNull();
        var (account, _) = altas.Created.ShouldHaveSingleItem();
        var (accountId, failedAt) = accounts.InvitationFailures.ShouldHaveSingleItem();

        accountId.ShouldBe(account.Id);
        failedAt.ShouldBe(StaffSamples.EstablishedAt);
        accounts.ClearedInvitationFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_inactive_specialty_is_rejected_before_writing_or_sending()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología", isActive: false);
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("inactiva@mousedent.test", [especialidad.Id], [local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.EspecialidadInactiva);
        altas.Created.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unknown_specialty_is_rejected_before_writing_or_sending()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(),
            new FakeCatalogo(),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("desconocida@mousedent.test", [Guid.NewGuid()], [local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.EspecialidadInactiva);
        altas.Created.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unknown_or_inactive_local_is_rejected_before_writing_or_sending()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var cerrada = StaffSamples.NewLocal("Sede Cerrada", isActive: false);
        var altas = new FakeAltaStore();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(cerrada),
            email: email);

        var inactivo = await service.RegisterAsync(
            ProfesionalSamples.Command("local.inactivo@mousedent.test", [especialidad.Id], [cerrada.Id]),
            Token);
        var inexistente = await service.RegisterAsync(
            ProfesionalSamples.Command("local.inexistente@mousedent.test", [especialidad.Id], [Guid.NewGuid()]),
            Token);

        inactivo.Status.ShouldBe(ProfesionalAltaStatus.LocalInactivo);
        inexistente.Status.ShouldBe(ProfesionalAltaStatus.LocalInactivo);
        altas.Created.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_ficha_without_locals_is_rejected_before_writing_or_sending()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var altas = new FakeAltaStore();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("sin.local@mousedent.test", [especialidad.Id], []),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.ActivacionIncompleta);
        altas.Created.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_colegiatura_over_the_limit_is_rejected_before_writing_or_sending()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command(
                "colegiatura.larga@mousedent.test",
                [especialidad.Id],
                [local.Id],
                new string('C', DatosDeColegiatura.LongitudMaxima + 1)),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.InvalidColegiatura);
        altas.Created.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_colegiatura_taken_reports_the_conflict_without_inviting()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore { Result = ResultadoEscrituraAlta.ColegiaturaDuplicada };
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("repetida@mousedent.test", [especialidad.Id], [local.Id], "COP 1"),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.DuplicateColegiatura);
        outcome.Ficha.ShouldBeNull();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_duplicate_email_found_on_write_reports_the_conflict_without_inviting()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore { Result = ResultadoEscrituraAlta.CorreoDuplicado };
        var accounts = new FakeStaffAccounts();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            accounts,
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("carrera@mousedent.test", [especialidad.Id], [local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.DuplicateEmail);
        accounts.InvitationFailures.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_email_of_another_profile_is_rejected_without_writing()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var reception = StaffSamples.Account(StaffRole.Reception, "recepcion@mousedent.test");
        var altas = new FakeAltaStore();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(reception),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("recepcion@mousedent.test", [especialidad.Id], [local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.WrongProfile);
        altas.Created.ShouldBeEmpty();
        altas.Linked.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_email_of_an_account_with_a_ficha_is_rejected_without_writing()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Account(StaffRole.Professional, "vinculada@mousedent.test");
        var altas = new FakeAltaStore();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(account),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            new FakeProfesionales(ProfesionalSamples.Ficha(account.Id, especialidad.Id, local.Id)),
            email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("vinculada@mousedent.test", [especialidad.Id], [local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.AccountAlreadyLinked);
        altas.Created.ShouldBeEmpty();
        altas.Linked.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_existing_professional_account_is_linked_without_touching_it_or_sending_email()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Pending(StaffRole.Professional, "pendiente@mousedent.test");
        var altas = new FakeAltaStore();
        var accounts = new FakeStaffAccounts(account);
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            accounts,
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command(
                "pendiente@mousedent.test",
                [especialidad.Id],
                [local.Id],
                displayName: "Nombre del alta"),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        var ficha = altas.Linked.ShouldHaveSingleItem();

        ficha.StaffAccountId.ShouldBe(account.Id);
        altas.Created.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
        accounts.Updated.ShouldBeEmpty();

        // El vínculo no cambia la cuenta: ni el nombre del alta, ni su estado, ni su sesión.
        account.DisplayName.ShouldBe("Persona pendiente");
        account.DisplayName.ShouldNotBe("Nombre del alta");
        account.SessionVersion.ShouldBe(1);
        account.IsPending.ShouldBeTrue();
        account.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Resending_a_pending_invitation_sends_it_and_clears_the_failure()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Pending(StaffRole.Professional, "pendiente@mousedent.test");
        var ficha = ProfesionalSamples.Ficha(account.Id, especialidad.Id, local.Id);
        var email = new FakeEmailSender();
        var accounts = new FakeStaffAccounts(account);
        var service = ProfesionalSamples.Service(
            new FakeAltaStore(),
            accounts,
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            new FakeProfesionales(ficha),
            email);

        var outcome = await service.ResendInvitationAsync(ficha.Id, Token);

        outcome.Status.ShouldBe(ProfesionalInvitationStatus.Sent);
        outcome.Account.ShouldNotBeNull().Id.ShouldBe(account.Id);
        email.Sent.ShouldHaveSingleItem().To.ShouldBe(account.Email);
        accounts.ClearedInvitationFailures.ShouldBe([account.Id]);
        accounts.InvitationFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resending_a_pending_invitation_records_the_failure_when_the_email_does_not_leave()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Pending(StaffRole.Professional, "pendiente@mousedent.test");
        var ficha = ProfesionalSamples.Ficha(account.Id, especialidad.Id, local.Id);
        var accounts = new FakeStaffAccounts(account);
        var service = ProfesionalSamples.Service(
            new FakeAltaStore(),
            accounts,
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            new FakeProfesionales(ficha),
            new FakeEmailSender { Fails = true });

        var outcome = await service.ResendInvitationAsync(ficha.Id, Token);

        outcome.Status.ShouldBe(ProfesionalInvitationStatus.DeliveryFailed);
        accounts.InvitationFailures.ShouldHaveSingleItem().AccountId.ShouldBe(account.Id);
        accounts.ClearedInvitationFailures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resending_the_invitation_of_an_account_with_a_password_is_rejected()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Account(StaffRole.Professional, "activa@mousedent.test");
        var ficha = ProfesionalSamples.Ficha(account.Id, especialidad.Id, local.Id);
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            new FakeAltaStore(),
            new FakeStaffAccounts(account),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            new FakeProfesionales(ficha),
            email);

        var outcome = await service.ResendInvitationAsync(ficha.Id, Token);

        outcome.Status.ShouldBe(ProfesionalInvitationStatus.PasswordAlreadyEstablished);
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resending_the_invitation_of_an_inactive_account_is_rejected()
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Pending(StaffRole.Professional, "inactiva@mousedent.test");
        account.Deactivate();
        var ficha = ProfesionalSamples.Ficha(account.Id, especialidad.Id, local.Id);
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            new FakeAltaStore(),
            new FakeStaffAccounts(account),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            new FakeProfesionales(ficha),
            email);

        var outcome = await service.ResendInvitationAsync(ficha.Id, Token);

        outcome.Status.ShouldBe(ProfesionalInvitationStatus.AccountInactive);
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resending_the_invitation_of_a_missing_ficha_is_rejected()
    {
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            new FakeAltaStore(),
            new FakeStaffAccounts(),
            new FakeCatalogo(),
            new FakeLocalStore(),
            email: email);

        var outcome = await service.ResendInvitationAsync(Guid.NewGuid(), Token);

        outcome.Status.ShouldBe(ProfesionalInvitationStatus.ProfessionalNotFound);
        email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_list_delegates_to_the_read_model_with_the_filter_it_received()
    {
        var account = StaffSamples.Pending(StaffRole.Professional, "listada@mousedent.test");
        var resumen = new ProfesionalResumen(
            Guid.NewGuid(),
            account.Id,
            "Persona listada",
            account.Email,
            null,
            "COP 1",
            IsActive: true,
            EstadoDeCuenta.Pendiente,
            InvitationFailed: true,
            [],
            []);
        var listado = new FakeListadoDeProfesionales(resumen);
        var service = ProfesionalSamples.Service(
            new FakeAltaStore(),
            new FakeStaffAccounts(account),
            new FakeCatalogo(),
            new FakeLocalStore(),
            listado: listado);
        var filtro = new ProfesionalFiltro
        {
            Search = "persona",
            AccountStatus = EstadoDeCuenta.Pendiente,
            FichaActiva = true,
        };

        var listed = await service.ListAsync(filtro, Token);

        listed.ShouldHaveSingleItem().ShouldBe(resumen);
        listado.Received.ShouldHaveSingleItem().ShouldBeSameAs(filtro);
    }

    [Theory]
    [InlineData(ResultadoEscrituraAlta.CuentaNoEncontrada, ProfesionalAltaStatus.AccountNotFound)]
    [InlineData(ResultadoEscrituraAlta.PerfilIncorrecto, ProfesionalAltaStatus.WrongProfile)]
    [InlineData(ResultadoEscrituraAlta.CuentaYaVinculada, ProfesionalAltaStatus.AccountAlreadyLinked)]
    public async Task A_write_rejection_of_the_link_is_reported_as_its_status(
        ResultadoEscrituraAlta writeResult,
        ProfesionalAltaStatus expected)
    {
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Pending(StaffRole.Professional, "vinculo@mousedent.test");
        var altas = new FakeAltaStore { Result = writeResult };
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            new FakeStaffAccounts(account),
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("vinculo@mousedent.test", [especialidad.Id], [local.Id]),
            Token);

        outcome.Status.ShouldBe(expected);
        outcome.Ficha.ShouldBeNull();
        altas.Linked.ShouldBeEmpty();
        email.Sent.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ResultadoEscrituraAlta.EspecialidadInactiva, ProfesionalAltaStatus.EspecialidadInactiva)]
    [InlineData(ResultadoEscrituraAlta.LocalInactivo, ProfesionalAltaStatus.LocalInactivo)]
    [InlineData(ResultadoEscrituraAlta.ActivacionIncompleta, ProfesionalAltaStatus.ActivacionIncompleta)]
    public async Task A_write_rejection_of_the_ficha_rules_is_reported_as_its_status(
        ResultadoEscrituraAlta writeResult,
        ProfesionalAltaStatus expected)
    {
        // El catálogo estaba activo al leer: la regla se descubre al escribir, no antes.
        var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
        var local = StaffSamples.NewLocal("Sede Centro");
        var altas = new FakeAltaStore { Result = writeResult };
        var accounts = new FakeStaffAccounts();
        var email = new FakeEmailSender();
        var service = ProfesionalSamples.Service(
            altas,
            accounts,
            new FakeCatalogo(especialidad),
            new FakeLocalStore(local),
            email: email);

        var outcome = await service.RegisterAsync(
            ProfesionalSamples.Command("regla@mousedent.test", [especialidad.Id], [local.Id]),
            Token);

        outcome.Status.ShouldBe(expected);
        outcome.Ficha.ShouldBeNull();
        email.Sent.ShouldBeEmpty();
        accounts.InvitationFailures.ShouldBeEmpty();
    }
}
