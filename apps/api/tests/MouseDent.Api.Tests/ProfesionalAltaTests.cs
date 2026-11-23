using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MouseDent.Application.Auth;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Persistence;
using MouseDent.Infrastructure.Profesionales;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Alta del profesional contra la API real: la cuenta y la ficha se guardan juntas,
/// la invitación usa el flujo de SPEC 02 y el fallo de su envío queda marcado en la
/// cuenta. El correo sale por el adaptador de prueba; el SMTP configurado se
/// comprueba a mano.
/// </summary>
public sealed class ProfesionalAltaTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    private const string NewPassword = "Profesional123!";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_email_creates_the_pending_account_the_ficha_and_the_invitation()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var email = StaffTestHelpers.UniqueEmail("alta.profesional");

        var outcome = await RegisterAsync(email, especialidad, local, "COP 12345");

        outcome.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        var ficha = outcome.Ficha.ShouldNotBeNull();

        var account = await AccountByEmailAsync(email);
        account.ShouldNotBeNull();
        account.Profile.ShouldBe(StaffRole.Professional);
        account.IsActive.ShouldBeTrue();
        account.PasswordEstablishedAt.ShouldBeNull();
        account.PhoneNumber.ShouldBeNull();
        account.DisplayName.ShouldBe("Profesional de pruebas");
        account.InvitationFailedAt.ShouldBeNull();
        (await LocalAssignmentsAsync(account.Id)).ShouldBeEmpty();

        var stored = await FichaRowAsync(ficha.Id);
        stored.ShouldNotBeNull();
        stored.StaffAccountId.ShouldBe(account.Id);
        stored.Colegiatura.ShouldBe("COP 12345");
        stored.ColegiaturaNormalizada.ShouldBe("COP12345");
        stored.IsActive.ShouldBeTrue();

        // El enlace se emitió con el token del correo y sirve una sola vez.
        var token = factory.Emails.LastTokenTo(email);
        token.ShouldNotBeNullOrWhiteSpace();
        factory.Emails.SentTo(email).ShouldHaveSingleItem().Body.ShouldContain(
            $"{MouseDentApiFactory.PublicBaseUrl}/establecer-contrasena?token={token}");

        using var anonymous = factory.CreateClient();
        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StaffTestHelpers.LoginAsync(anonymous, email, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_failed_invitation_keeps_the_ficha_and_the_pending_account_and_the_resend_clears_it()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var email = StaffTestHelpers.UniqueEmail("alta.fallida");
        factory.Emails.FailingAddresses.Add(email);

        var outcome = await RegisterAsync(email, especialidad, local);
        factory.Emails.FailingAddresses.Remove(email);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.InvitationFailed);
        var ficha = outcome.Ficha.ShouldNotBeNull();
        var account = await AccountByEmailAsync(email);
        account.ShouldNotBeNull();
        account.PasswordEstablishedAt.ShouldBeNull();
        account.IsActive.ShouldBeTrue();
        account.InvitationFailedAt.ShouldNotBeNull();
        account.SessionVersion.ShouldBe(1);
        (await FichaRowAsync(ficha.Id)).ShouldNotBeNull();
        factory.Emails.SentTo(email).ShouldBeEmpty();

        var resend = await ResendAsync(ficha.Id);

        resend.Status.ShouldBe(ProfesionalInvitationStatus.Sent);
        (await AccountByEmailAsync(email))!.InvitationFailedAt.ShouldBeNull();
        var token = factory.Emails.LastTokenTo(email);
        token.ShouldNotBeNullOrWhiteSpace();

        using var anonymous = factory.CreateClient();
        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_unexpected_failure_of_the_send_counts_as_an_invitation_that_did_not_leave()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var email = StaffTestHelpers.UniqueEmail("alta.error");
        factory.Emails.BrokenAdapterAddresses.Add(email);

        var outcome = await RegisterAsync(email, especialidad, local);
        factory.Emails.BrokenAdapterAddresses.Remove(email);

        // La cuenta y la ficha ya estaban confirmadas: el fallo no las deshace y deja la marca.
        outcome.Status.ShouldBe(ProfesionalAltaStatus.InvitationFailed);
        var ficha = outcome.Ficha.ShouldNotBeNull();
        (await FichaRowAsync(ficha.Id)).ShouldNotBeNull();
        var account = await AccountByEmailAsync(email);
        account.ShouldNotBeNull();
        account.InvitationFailedAt.ShouldNotBeNull();
        account.SessionVersion.ShouldBe(1);
        factory.Emails.SentTo(email).ShouldBeEmpty();

        // El reenvío traduce igual un fallo que no es del protocolo y conserva la marca.
        factory.Emails.BrokenAdapterAddresses.Add(email);
        var resend = await ResendAsync(ficha.Id);
        factory.Emails.BrokenAdapterAddresses.Remove(email);

        resend.Status.ShouldBe(ProfesionalInvitationStatus.DeliveryFailed);
        (await AccountByEmailAsync(email))!.InvitationFailedAt.ShouldNotBeNull();
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_pending_account_without_ficha_is_linked_without_sending_email()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var email = StaffTestHelpers.UniqueEmail("vinculo.pendiente");
        var accountId = await SeedPendingProfessionalAsync(email);

        var outcome = await RegisterAsync(email, especialidad, local);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        var ficha = outcome.Ficha.ShouldNotBeNull();
        (await FichaRowAsync(ficha.Id))!.StaffAccountId.ShouldBe(accountId);

        // El vínculo no toca la cuenta ni envía correo.
        var account = await AccountByEmailAsync(email);
        account.ShouldNotBeNull();
        account.DisplayName.ShouldBe("Cuenta pendiente de pruebas");
        account.PasswordEstablishedAt.ShouldBeNull();
        account.SessionVersion.ShouldBe(1);
        account.InvitationFailedAt.ShouldBeNull();
        factory.Emails.SentTo(email).ShouldBeEmpty();

        // La ficha ofrece el reenvío de la invitación pendiente.
        var resend = await ResendAsync(ficha.Id);

        resend.Status.ShouldBe(ProfesionalInvitationStatus.Sent);
        factory.Emails.SentTo(email).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_linked_account_with_a_password_keeps_its_session_valid()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var email = StaffTestHelpers.UniqueEmail("vinculo.activa");
        var accountId = await factory.SeedAccountAsync(email, NewPassword, StaffRole.Professional);
        var token = await factory.LoginAsync(email, NewPassword);

        using (var before = factory.WithToken(token))
        {
            (await before.GetAsync("/api/auth/me", Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var outcome = await RegisterAsync(email, especialidad, local);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        var ficha = outcome.Ficha.ShouldNotBeNull();
        (await FichaRowAsync(ficha.Id))!.StaffAccountId.ShouldBe(accountId);

        // Ni el estado, ni la contraseña, ni la versión de sesión cambian, y no hay correo.
        var account = await AccountByIdAsync(accountId);
        account.ShouldNotBeNull();
        account.IsActive.ShouldBeTrue();
        account.PasswordEstablishedAt.ShouldNotBeNull();
        account.SessionVersion.ShouldBe(1);
        account.DisplayName.ShouldBe("Profesional de pruebas");
        factory.Emails.SentTo(email).ShouldBeEmpty();

        using (var after = factory.WithToken(token))
        {
            (await after.GetAsync("/api/auth/me", Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await ResendAsync(ficha.Id)).Status
            .ShouldBe(ProfesionalInvitationStatus.PasswordAlreadyEstablished);
    }

    [Fact]
    public async Task An_email_of_another_profile_or_of_a_linked_account_is_rejected_without_partial_data()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();

        var reception = StaffTestHelpers.UniqueEmail("perfil.recepcion");
        await factory.SeedAccountAsync(reception, "Recepcion123!", StaffRole.Reception);

        var otherProfile = await RegisterAsync(reception, especialidad, local);

        otherProfile.Status.ShouldBe(ProfesionalAltaStatus.WrongProfile);
        (await CountFichasOfEmailAsync(reception)).ShouldBe(0);
        factory.Emails.SentTo(reception).ShouldBeEmpty();

        var linked = StaffTestHelpers.UniqueEmail("perfil.conficha");
        var first = await RegisterAsync(linked, especialidad, local);
        first.Status.ShouldBe(ProfesionalAltaStatus.Saved);

        var second = await RegisterAsync(linked, especialidad, local, "COP " + Guid.NewGuid().ToString("N")[..8]);

        second.Status.ShouldBe(ProfesionalAltaStatus.AccountAlreadyLinked);
        (await CountFichasOfEmailAsync(linked)).ShouldBe(1);
        factory.Emails.SentTo(linked).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_duplicate_colegiatura_is_rejected_without_creating_the_account()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var colegiatura = "COP " + Guid.NewGuid().ToString("N")[..8];

        var first = await RegisterAsync(StaffTestHelpers.UniqueEmail("colegiatura.primera"), especialidad, local, colegiatura);
        first.Status.ShouldBe(ProfesionalAltaStatus.Saved);

        var repeatedEmail = StaffTestHelpers.UniqueEmail("colegiatura.repetida");
        var repeated = await RegisterAsync(
            repeatedEmail,
            especialidad,
            local,
            colegiatura.ToLowerInvariant());

        repeated.Status.ShouldBe(ProfesionalAltaStatus.DuplicateColegiatura);
        repeated.Ficha.ShouldBeNull();
        (await AccountByEmailAsync(repeatedEmail)).ShouldBeNull();
        factory.Emails.SentTo(repeatedEmail).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_linked_account_rejected_by_the_colegiatura_leaves_no_ficha_and_keeps_the_account()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var colegiatura = "COP " + Guid.NewGuid().ToString("N")[..8];

        var first = await RegisterAsync(
            StaffTestHelpers.UniqueEmail("vinculo.colegiatura"),
            especialidad,
            local,
            colegiatura);
        first.Status.ShouldBe(ProfesionalAltaStatus.Saved);

        // La cuenta existe desde antes, sin ficha: el rechazo llega en la escritura de
        // la ficha y la transacción tiene que deshacerla.
        var email = StaffTestHelpers.UniqueEmail("vinculo.repetida");
        var accountId = await SeedPendingProfessionalAsync(email);

        var repeated = await RegisterAsync(email, especialidad, local, colegiatura.ToLowerInvariant());

        repeated.Status.ShouldBe(ProfesionalAltaStatus.DuplicateColegiatura);
        repeated.Ficha.ShouldBeNull();
        (await CountFichasOfEmailAsync(email)).ShouldBe(0);
        factory.Emails.SentTo(email).ShouldBeEmpty();

        var account = await AccountByIdAsync(accountId);
        account.ShouldNotBeNull();
        account.Profile.ShouldBe(StaffRole.Professional);
        account.IsActive.ShouldBeTrue();
        account.PasswordEstablishedAt.ShouldBeNull();
        account.SessionVersion.ShouldBe(1);
        account.InvitationFailedAt.ShouldBeNull();
        account.DisplayName.ShouldBe("Cuenta pendiente de pruebas");
    }

    [Fact]
    public async Task The_failure_mark_is_cleared_when_the_password_is_set_and_when_the_email_changes()
    {
        var especialidad = await SeedEspecialidadAsync();
        var local = await SeedLocalAsync();
        var email = StaffTestHelpers.UniqueEmail("marca.contrasena");
        factory.Emails.FailingAddresses.Add(email);
        var outcome = await RegisterAsync(email, especialidad, local);
        factory.Emails.FailingAddresses.Remove(email);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.InvitationFailed);
        var marked = await AccountByEmailAsync(email);
        marked.ShouldNotBeNull();
        marked.InvitationFailedAt.ShouldNotBeNull();
        marked.SessionVersion.ShouldBe(1);

        // La recuperación es otro correo: no limpia la marca de la invitación.
        using var anonymous = factory.CreateClient();
        (await StaffTestHelpers.RequestRecoveryAsync(anonymous, email)).StatusCode
            .ShouldBe(HttpStatusCode.Accepted);
        (await AccountByEmailAsync(email))!.InvitationFailedAt.ShouldNotBeNull();

        var token = factory.Emails.LastTokenTo(email);
        token.ShouldNotBeNullOrWhiteSpace();
        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AccountByEmailAsync(email))!.InvitationFailedAt.ShouldBeNull();

        var changedEmail = StaffTestHelpers.UniqueEmail("marca.correo");
        factory.Emails.FailingAddresses.Add(changedEmail);
        var second = await RegisterAsync(changedEmail, especialidad, local);
        factory.Emails.FailingAddresses.Remove(changedEmail);
        second.Status.ShouldBe(ProfesionalAltaStatus.InvitationFailed);
        (await AccountByEmailAsync(changedEmail))!.InvitationFailedAt.ShouldNotBeNull();

        var newEmail = StaffTestHelpers.UniqueEmail("marca.correo.nuevo");
        using var admin = await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            second.Account!.Id,
            newEmail,
            StaffRole.Professional);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var changed = await AccountByEmailAsync(newEmail);
        changed.ShouldNotBeNull();
        changed.InvitationFailedAt.ShouldBeNull();
    }

    [Fact]
    public async Task The_list_summary_reports_the_ficha_the_account_state_and_the_failed_invitation()
    {
        var especialidad = await SeedEspecialidadAsync();
        var deactivatedEspecialidad = await SeedEspecialidadAsync();
        var deactivatedLocal = await SeedLocalAsync();
        var local = await SeedLocalAsync();

        // Ficha activa con cuenta pendiente y correo fallido.
        var pendingEmail = StaffTestHelpers.UniqueEmail("resumen.pendiente");
        factory.Emails.FailingAddresses.Add(pendingEmail);
        var pending = await RegisterAsync(pendingEmail, especialidad, local, "COP 777");
        factory.Emails.FailingAddresses.Remove(pendingEmail);
        pending.Status.ShouldBe(ProfesionalAltaStatus.InvitationFailed);
        var pendingFicha = pending.Ficha.ShouldNotBeNull();

        // Ficha activa con cuenta activa y una especialidad que después se desactiva.
        var activeEmail = StaffTestHelpers.UniqueEmail("resumen.activa");
        var active = await RegisterAsync(activeEmail, deactivatedEspecialidad, local);
        active.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        var activeFicha = active.Ficha.ShouldNotBeNull();
        await SetEspecialidadStatusAsync(deactivatedEspecialidad, isActive: false);
        var activeToken = factory.Emails.LastTokenTo(activeEmail);
        using (var anonymous = factory.CreateClient())
        {
            using var completion = await StaffTestHelpers.ResetPasswordAsync(
                anonymous,
                activeToken!,
                NewPassword);
            completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Ficha inactiva con cuenta desactivada sin contraseña.
        var inactiveEmail = StaffTestHelpers.UniqueEmail("resumen.inactiva");
        await SeedPendingProfessionalAsync(inactiveEmail, isActive: false);
        var inactive = await RegisterAsync(inactiveEmail, especialidad, deactivatedLocal);
        inactive.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        var inactiveFicha = inactive.Ficha.ShouldNotBeNull();
        await SetFichaStatusAsync(inactiveFicha.Id, isActive: false);
        await SetLocalStatusAsync(deactivatedLocal, isActive: false);

        var pendingSummary = (await ListAsync(new ProfesionalFiltro { Search = pendingEmail }))
            .ShouldHaveSingleItem();
        pendingSummary.Id.ShouldBe(pendingFicha.Id);
        pendingSummary.StaffAccountId.ShouldBe(pending.Account!.Id);
        pendingSummary.DisplayName.ShouldBe("Profesional de pruebas");
        pendingSummary.Email.ShouldBe(pendingEmail);
        pendingSummary.Phone.ShouldBeNull();
        pendingSummary.Colegiatura.ShouldBe("COP 777");
        pendingSummary.IsActive.ShouldBeTrue();
        pendingSummary.AccountStatus.ShouldBe(EstadoDeCuenta.Pendiente);
        pendingSummary.InvitationFailed.ShouldBeTrue();
        pendingSummary.Especialidades.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            especialidadResumen => especialidadResumen.Id.ShouldBe(especialidad),
            especialidadResumen => especialidadResumen.IsActive.ShouldBeTrue());
        pendingSummary.Locales.ShouldHaveSingleItem().Id.ShouldBe(local);

        var activeSummary = (await ListAsync(new ProfesionalFiltro { Search = activeEmail }))
            .ShouldHaveSingleItem();
        activeSummary.AccountStatus.ShouldBe(EstadoDeCuenta.Activa);
        activeSummary.InvitationFailed.ShouldBeFalse();
        activeSummary.Especialidades.ShouldHaveSingleItem().IsActive.ShouldBeFalse();

        var inactiveSummary = (await ListAsync(new ProfesionalFiltro { Search = inactiveEmail }))
            .ShouldHaveSingleItem();
        inactiveSummary.AccountStatus.ShouldBe(EstadoDeCuenta.Inactiva);
        inactiveSummary.InvitationFailed.ShouldBeFalse();
        inactiveSummary.IsActive.ShouldBeFalse();
        inactiveSummary.Locales.ShouldHaveSingleItem().IsActive.ShouldBeFalse();

        // Búsqueda por nombre y filtros por cuenta, ficha, especialidad y local.
        (await ListAsync(new ProfesionalFiltro { Search = "Profesional de pruebas" }))
            .ShouldContain(resumen => resumen.Id == pendingFicha.Id);

        var byPendingAccount = await ListAsync(new ProfesionalFiltro { AccountStatus = EstadoDeCuenta.Pendiente });
        byPendingAccount.ShouldContain(resumen => resumen.Id == pendingFicha.Id);
        byPendingAccount.ShouldNotContain(resumen => resumen.Id == activeFicha.Id);
        byPendingAccount.ShouldAllBe(resumen => resumen.AccountStatus == EstadoDeCuenta.Pendiente);

        var inactiveAccounts = await ListAsync(new ProfesionalFiltro { AccountStatus = EstadoDeCuenta.Inactiva });
        inactiveAccounts.ShouldContain(resumen => resumen.Id == inactiveFicha.Id);
        inactiveAccounts.ShouldAllBe(resumen => resumen.AccountStatus == EstadoDeCuenta.Inactiva);

        var activeAccounts = await ListAsync(new ProfesionalFiltro { AccountStatus = EstadoDeCuenta.Activa });
        activeAccounts.ShouldContain(resumen => resumen.Id == activeFicha.Id);
        activeAccounts.ShouldNotContain(resumen => resumen.Id == pendingFicha.Id);
        activeAccounts.ShouldAllBe(resumen => resumen.AccountStatus == EstadoDeCuenta.Activa);

        var inactiveFichas = await ListAsync(new ProfesionalFiltro { FichaActiva = false });
        inactiveFichas.ShouldContain(resumen => resumen.Id == inactiveFicha.Id);
        inactiveFichas.ShouldNotContain(resumen => resumen.Id == pendingFicha.Id);
        inactiveFichas.ShouldAllBe(resumen => !resumen.IsActive);

        (await ListAsync(new ProfesionalFiltro { EspecialidadId = deactivatedEspecialidad }))
            .ShouldContain(resumen => resumen.Id == activeFicha.Id);

        (await ListAsync(new ProfesionalFiltro { LocalId = deactivatedLocal }))
            .ShouldContain(resumen => resumen.Id == inactiveFicha.Id);
    }

    private Task<ProfesionalAltaOutcome> RegisterAsync(
        string email,
        Guid especialidadId,
        Guid localId,
        string? colegiatura = null) =>
        RegisterAsync(new ProfesionalAltaCommand(
            "Profesional de pruebas",
            email,
            colegiatura,
            [especialidadId],
            [localId]));

    private async Task<ProfesionalAltaOutcome> RegisterAsync(ProfesionalAltaCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProfesionalManagementService>();

        return await service.RegisterAsync(command, Token);
    }

    private async Task<ProfesionalInvitationOutcome> ResendAsync(Guid profesionalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProfesionalManagementService>();

        return await service.ResendInvitationAsync(profesionalId, Token);
    }

    private async Task<IReadOnlyList<ProfesionalResumen>> ListAsync(ProfesionalFiltro filtro)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProfesionalManagementService>();

        return await service.ListAsync(filtro, Token);
    }

    private async Task<Guid> SeedEspecialidadAsync(string? name = null, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var especialidad = Especialidad.Create(name ?? $"Especialidad {Guid.NewGuid():N}", null);
        if (!isActive)
        {
            especialidad.Deactivate();
        }

        database.Especialidades.Add(especialidad);
        await database.SaveChangesAsync(Token);

        return especialidad.Id;
    }

    private async Task<Guid> SeedLocalAsync(string? name = null, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var schedule = WeeklySchedule.Parse(WeeklySchedule.WeekDays.Select(day =>
            new ScheduleDayInput(day, false, new TimeOnly(9, 0), new TimeOnly(18, 0)))).Schedule!;
        var local = Local.Create(
            name ?? $"Sede {Guid.NewGuid():N}",
            "Av. Siempre Viva 742",
            "+51 999 888 777",
            schedule);

        if (!isActive)
        {
            local.Deactivate();
        }

        database.Locales.Add(local);
        await database.SaveChangesAsync(Token);

        return local.Id;
    }

    /// <summary>Crea una cuenta con perfil Profesional que nunca estableció contraseña.</summary>
    private async Task<Guid> SeedPendingProfessionalAsync(string email, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var account = new StaffUser
        {
            UserName = Guid.NewGuid().ToString(),
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Cuenta pendiente de pruebas",
            Profile = StaffRole.Professional,
            IsActive = isActive,
        };

        var created = await users.CreateAsync(account);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear la cuenta de pruebas: {string.Join("; ", created.Errors.Select(error => error.Description))}");
        }

        return account.Id;
    }

    private async Task SetEspecialidadStatusAsync(Guid especialidadId, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var especialidad = await database.Especialidades.SingleAsync(
            stored => stored.Id == especialidadId,
            Token);

        if (isActive)
        {
            especialidad.Activate();
        }
        else
        {
            especialidad.Deactivate();
        }

        await database.SaveChangesAsync(Token);
    }

    private async Task SetLocalStatusAsync(Guid localId, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var local = await database.Locales.SingleAsync(stored => stored.Id == localId, Token);

        if (isActive)
        {
            local.Activate();
        }
        else
        {
            local.Deactivate();
        }

        await database.SaveChangesAsync(Token);
    }

    private async Task SetFichaStatusAsync(Guid profesionalId, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var ficha = await database.Profesionales.SingleAsync(stored => stored.Id == profesionalId, Token);
        ficha.IsActive = isActive;
        await database.SaveChangesAsync(Token);
    }

    private async Task<StaffUser?> AccountByIdAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();

        return await database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, Token);
    }

    private async Task<StaffUser?> AccountByEmailAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();

        return await database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == email, Token);
    }

    private async Task<IReadOnlyList<Guid>> LocalAssignmentsAsync(Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();

        return await database.Set<StaffLocalAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == accountId)
            .Select(assignment => assignment.LocalId)
            .ToListAsync(Token);
    }

    private async Task<ProfesionalGuardado?> FichaRowAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();

        return await database.Profesionales
            .AsNoTracking()
            .Include(ficha => ficha.Especialidades)
            .Include(ficha => ficha.Locales)
            .SingleOrDefaultAsync(ficha => ficha.Id == id, Token);
    }

    private async Task<int> CountFichasOfEmailAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var account = await database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == email, Token);

        return account is null
            ? 0
            : await database.Profesionales.CountAsync(
                ficha => ficha.StaffAccountId == account.Id,
                Token);
    }
}

/// <summary>
/// Alta simultánea con el mismo correo. Necesita conexiones independientes, así que
/// usa la base en archivo: una solicitud gana el correo y la otra se rechaza, sin
/// dejar dos cuentas ni dos fichas. El motivo del rechazo depende del intercalado: si
/// la solicitud perdedora lee antes del commit choca con el índice único del correo y
/// si lee después entra por el vínculo y encuentra la cuenta ya vinculada. Las dos
/// respuestas son correctas, así que la prueba asevera la invariante en lugar de
/// fijar un camino.
/// </summary>
public sealed class ProfesionalAltaConcurrencyTests(MouseDentApiFileDatabaseFactory factory)
    : IClassFixture<MouseDentApiFileDatabaseFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_simultaneous_alta_with_the_same_email_leave_one_account_and_one_ficha()
    {
        var (especialidad, local) = await AltaConcurrency.SeedAsync(factory);
        var email = StaffTestHelpers.UniqueEmail("alta.simultanea");

        using var gate = new ManualResetEventSlim();

        async Task<ProfesionalAltaOutcome> RegisterAsync(string colegiatura)
        {
            await Task.Yield();
            gate.Wait(Token);

            return await AltaConcurrency.RegisterAsync(factory, email, especialidad, local, colegiatura);
        }

        var first = Task.Run(() => RegisterAsync("COP SIM 1"), Token);
        var second = Task.Run(() => RegisterAsync("COP SIM 2"), Token);
        gate.Set();

        var results = await Task.WhenAll(first, second);

        results.Count(outcome => outcome.Status is ProfesionalAltaStatus.Saved).ShouldBe(1);
        var rejected = results
            .Where(outcome => outcome.Status is not ProfesionalAltaStatus.Saved)
            .ShouldHaveSingleItem();
        rejected.Status.ShouldBeOneOf(
            ProfesionalAltaStatus.DuplicateEmail,
            ProfesionalAltaStatus.AccountAlreadyLinked);
        rejected.Ficha.ShouldBeNull();

        await AltaConcurrency.AssertOneAccountAndOneFichaAsync(factory, email);
        factory.Emails.SentTo(email).ShouldHaveSingleItem();
    }
}

/// <summary>
/// El mismo alta simultánea, con el intercalado forzado: la solicitud perdedora hace
/// su lectura después de que la ganadora confirmó, así que entra por el vínculo y
/// encuentra la cuenta con ficha. Fija el rechazo que la prueba concurrente deja al
/// azar y comprueba que la invariante se mantiene igual.
/// </summary>
public sealed class ProfesionalAltaInterleavingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_alta_that_reads_after_the_winner_committed_is_rejected_as_already_linked()
    {
        using var factory = new InterleavingAltaFactory();
        var (especialidad, local) = await AltaConcurrency.SeedAsync(factory);
        var email = StaffTestHelpers.UniqueEmail("alta.intercalada");

        ProfesionalAltaOutcome? winner = null;
        factory.BeforeFirstRead = async () =>
            winner = await AltaConcurrency.RegisterAsync(factory, email, especialidad, local, "COP INT 1");

        var loser = await AltaConcurrency.RegisterAsync(factory, email, especialidad, local, "COP INT 2");

        winner.ShouldNotBeNull();
        winner.Status.ShouldBe(ProfesionalAltaStatus.Saved);
        loser.Status.ShouldBe(ProfesionalAltaStatus.AccountAlreadyLinked);
        loser.Ficha.ShouldBeNull();

        await AltaConcurrency.AssertOneAccountAndOneFichaAsync(factory, email);
        factory.Emails.SentTo(email).ShouldHaveSingleItem();
    }

    /// <summary>
    /// Decorado sobre el almacén real de cuentas: la primera lectura de la cuenta corre
    /// un alta completa antes de devolver su resultado, de modo que quien lee encuentra
    /// lo que la otra solicitud ya confirmó.
    /// </summary>
    private sealed class InterleavingAltaFactory() : MouseDentApiFactoryBase(useFileDatabase: true)
    {
        private Func<Task>? _beforeFirstRead;

        public Func<Task>? BeforeFirstRead { set => _beforeFirstRead = value; }

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IStaffAccounts>();
            services.AddScoped<EfStaffAccounts>();
            services.AddScoped<IStaffAccounts>(provider => new InterleavingAccounts(
                provider.GetRequiredService<EfStaffAccounts>())
            {
                BeforeFirstRead = async () =>
                {
                    var action = Interlocked.Exchange(ref _beforeFirstRead, null);
                    if (action is not null)
                    {
                        await action();
                    }
                },
            });
        }
    }

    private sealed class InterleavingAccounts(IStaffAccounts inner) : IStaffAccounts
    {
        private Func<Task>? _beforeFirstRead;

        public Func<Task>? BeforeFirstRead { set => _beforeFirstRead = value; }

        public async Task<StaffAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            var interleave = Interlocked.Exchange(ref _beforeFirstRead, null);
            if (interleave is not null)
            {
                await interleave();
            }

            return await inner.FindByEmailAsync(email, cancellationToken);
        }

        public Task<StaffAccount?> ValidateCredentialsAsync(
            string email,
            string password,
            CancellationToken cancellationToken) =>
            inner.ValidateCredentialsAsync(email, password, cancellationToken);

        public Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            inner.FindByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<StaffAccount>> ListAsync(
            string? search,
            CancellationToken cancellationToken) =>
            inner.ListAsync(search, cancellationToken);

        public Task<StaffSaveResult> CreateAsync(StaffAccount account, CancellationToken cancellationToken) =>
            inner.CreateAsync(account, cancellationToken);

        public Task<StaffSaveResult> UpdateAsync(
            StaffAccount account,
            StaffAccount expected,
            CancellationToken cancellationToken) =>
            inner.UpdateAsync(account, expected, cancellationToken);

        public Task RecordInvitationFailureAsync(
            Guid accountId,
            DateTime failedAt,
            CancellationToken cancellationToken) =>
            inner.RecordInvitationFailureAsync(accountId, failedAt, cancellationToken);

        public Task ClearInvitationFailureAsync(Guid accountId, CancellationToken cancellationToken) =>
            inner.ClearInvitationFailureAsync(accountId, cancellationToken);
    }
}

/// <summary>Siembra y alta compartidas por las pruebas de concurrencia del alta.</summary>
internal static class AltaConcurrency
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<(Guid Especialidad, Guid Local)> SeedAsync(MouseDentApiFactoryBase factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var especialidad = Especialidad.Create($"Especialidad {Guid.NewGuid():N}", null);
        var schedule = WeeklySchedule.Parse(WeeklySchedule.WeekDays.Select(day =>
            new ScheduleDayInput(day, false, new TimeOnly(9, 0), new TimeOnly(18, 0)))).Schedule!;
        var local = Local.Create($"Sede {Guid.NewGuid():N}", "Av. Siempre Viva 742", "+51 999 888 777", schedule);

        database.Especialidades.Add(especialidad);
        database.Locales.Add(local);
        await database.SaveChangesAsync(Token);

        return (especialidad.Id, local.Id);
    }

    public static async Task<ProfesionalAltaOutcome> RegisterAsync(
        MouseDentApiFactoryBase factory,
        string email,
        Guid especialidadId,
        Guid localId,
        string? colegiatura)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProfesionalManagementService>();

        return await service.RegisterAsync(
            new ProfesionalAltaCommand(
                "Profesional simultáneo",
                email,
                colegiatura,
                [especialidadId],
                [localId]),
            Token);
    }

    /// <summary>La invariante del alta: una cuenta pendiente, una ficha y la cuenta como estaba.</summary>
    public static async Task AssertOneAccountAndOneFichaAsync(MouseDentApiFactoryBase factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var accounts = await database.Users
            .AsNoTracking()
            .Where(user => user.Email == email)
            .ToListAsync(Token);
        var account = accounts.ShouldHaveSingleItem();

        account.PasswordEstablishedAt.ShouldBeNull();
        (await database.Profesionales.CountAsync(ficha => ficha.StaffAccountId == account.Id, Token))
            .ShouldBe(1);
    }
}
