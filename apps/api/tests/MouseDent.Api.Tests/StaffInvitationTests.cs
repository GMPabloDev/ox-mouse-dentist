using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MouseDent.Api.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Invitaciones de alta y de cambio de correo comprobadas por la API: destino y
/// contenido del correo, uso único, vigencia, reenvío y fallos de envío. El
/// correo sale por un adaptador de prueba; el SMTP configurado se comprueba a mano.
/// </summary>
public sealed class StaffInvitationTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    private const string NewPassword = "Recepcion123!";

    [Fact]
    public async Task A_new_account_receives_an_invitation_and_uses_the_link_once()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("invitacion.alta");
        var account = await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var token = factory.Emails.LastTokenTo(email);

        token.ShouldNotBeNullOrWhiteSpace();
        account.Status.ShouldBe(StaffAccountState.Pendiente);

        var sent = factory.Emails.SentTo(email).ShouldHaveSingleItem();
        sent.Subject.ShouldContain("invitación");
        sent.Body.ShouldContain(
            $"{MouseDentApiFactory.PublicBaseUrl}/establecer-contrasena?token={token}");

        using var anonymous = factory.CreateClient();
        (await StaffTestHelpers.LoginAsync(anonymous, email, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await StaffTestHelpers.LoginAsync(anonymous, email, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // El mismo enlace no sirve dos veces y el rechazo no repite el token.
        using var replayed = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, "OtraClave123!");
        replayed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await replayed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldNotContain(token!);
    }

    [Fact]
    public async Task A_weak_password_is_rejected_with_the_requirements_and_the_account_stays_pending()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("invitacion.debil");
        await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var token = factory.Emails.LastTokenTo(email);

        // Cumple el formulario de acceso pero no la política: falta una mayúscula.
        const string WeakPassword = "sinmayuscula1!";

        using var anonymous = factory.CreateClient();
        using var weak = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, WeakPassword);

        weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = await StaffTestHelpers.ReadValidationProblemAsync(weak);
        errors.ShouldContainKey(nameof(ResetPasswordRequest.Password));
        errors[nameof(ResetPasswordRequest.Password)].ShouldNotBeEmpty();

        (await StaffTestHelpers.LoginAsync(anonymous, email, WeakPassword))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var strong = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        strong.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_failed_invitation_keeps_the_account_pending_and_allows_resending_it()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("invitacion.fallida");
        factory.Emails.FailingAddresses.Add(email);

        using var creation = await StaffTestHelpers.CreateStaffAsync(admin, email, StaffRole.Professional);
        creation.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var createdId = await ProblemAccountIdAsync(creation);

        factory.Emails.SentTo(email).ShouldBeEmpty();
        var listed = await StaffTestHelpers.FindStaffAsync(admin, email);
        listed.Id.ShouldBe(createdId);
        listed.Status.ShouldBe(StaffAccountState.Pendiente);
        (await StaffTestHelpers.ListStaffAsync(admin, email)).Count.ShouldBe(1);

        // Administración ve el pendiente y reenvía sin crear otra cuenta.
        factory.Emails.FailingAddresses.Remove(email);
        using var resend = await StaffTestHelpers.ResendInvitationAsync(admin, createdId);
        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = factory.Emails.LastTokenTo(email);

        using var anonymous = factory.CreateClient();
        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StaffTestHelpers.LoginAsync(anonymous, email, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resending_an_invitation_invalidates_the_previous_link()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("invitacion.reenvio");
        var account = await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var first = factory.Emails.LastTokenTo(email);

        using var resend = await StaffTestHelpers.ResendInvitationAsync(admin, account.Id);
        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = factory.Emails.LastTokenTo(email);

        second.ShouldNotBe(first);

        using var anonymous = factory.CreateClient();
        using var oldLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, first!, NewPassword);
        oldLink.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var newLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, second!, NewPassword);
        newLink.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_failed_resend_still_invalidates_the_previous_link()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("invitacion.reenvio.fallido");
        var account = await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var first = factory.Emails.LastTokenTo(email);

        factory.Emails.FailingAddresses.Add(email);
        using var failed = await StaffTestHelpers.ResendInvitationAsync(admin, account.Id);
        failed.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

        // El enlace nuevo no llegó, pero el anterior tampoco sirve.
        using var anonymous = factory.CreateClient();
        using var oldLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, first!, NewPassword);
        oldLink.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        factory.Emails.FailingAddresses.Remove(email);
        using var resent = await StaffTestHelpers.ResendInvitationAsync(admin, account.Id);
        resent.StatusCode.ShouldBe(HttpStatusCode.OK);
        var third = factory.Emails.LastTokenTo(email);

        using var newLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, third!, NewPassword);
        newLink.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Changing_the_email_of_a_pending_account_replaces_its_invitation()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("correo.anterior");
        var newEmail = StaffTestHelpers.UniqueEmail("correo.nuevo");
        var account = await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var oldToken = factory.Emails.LastTokenTo(email);

        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            account.Id,
            newEmail,
            StaffRole.Professional);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StaffTestHelpers.ReadStaffAsync(update)).Status.ShouldBe(StaffAccountState.Pendiente);

        var newToken = factory.Emails.LastTokenTo(newEmail);
        newToken.ShouldNotBeNullOrWhiteSpace();
        factory.Emails.SentTo(email).Count.ShouldBe(1);

        using var anonymous = factory.CreateClient();
        using var oldLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, oldToken!, NewPassword);
        oldLink.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var newLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, newToken!, NewPassword);
        newLink.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await StaffTestHelpers.LoginAsync(anonymous, newEmail, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_the_email_of_an_active_account_revokes_its_session_and_invites_the_new_address()
    {
        var email = StaffTestHelpers.UniqueEmail("correo.sesion");
        var newEmail = StaffTestHelpers.UniqueEmail("correo.sesion.nuevo");
        var accountId = await factory.SeedAccountAsync(email, NewPassword, StaffRole.Reception);
        var sessionToken = await factory.LoginAsync(email, NewPassword);
        using var session = factory.WithToken(sessionToken);

        using var admin = await AdminAsync();
        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            newEmail,
            StaffRole.Reception);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var after = await session.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newToken = factory.Emails.LastTokenTo(newEmail);
        newToken.ShouldNotBeNullOrWhiteSpace();

        using var anonymous = factory.CreateClient();
        foreach (var candidate in new[] { email, newEmail })
        {
            (await StaffTestHelpers.LoginAsync(anonymous, candidate, NewPassword))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized, candidate);
        }

        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, newToken!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StaffTestHelpers.LoginAsync(anonymous, newEmail, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_failed_invitation_after_an_email_change_keeps_the_account_pending()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("correo.fallo");
        var newEmail = StaffTestHelpers.UniqueEmail("correo.fallo.nuevo");
        var account = await StaffTestHelpers.CreatePendingStaffAsync(admin, email);

        factory.Emails.FailingAddresses.Add(newEmail);
        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            account.Id,
            newEmail,
            StaffRole.Professional);
        update.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

        var stored = await StaffTestHelpers.FindStaffAsync(admin, newEmail);
        stored.Status.ShouldBe(StaffAccountState.Pendiente);
        factory.Emails.SentTo(newEmail).ShouldBeEmpty();

        factory.Emails.FailingAddresses.Remove(newEmail);
        using var resend = await StaffTestHelpers.ResendInvitationAsync(admin, stored.Id);
        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = factory.Emails.LastTokenTo(newEmail);

        using var anonymous = factory.CreateClient();
        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deactivating_an_account_invalidates_its_links_and_reactivation_allows_a_new_invitation()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("invitacion.desactivada");
        var account = await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var first = factory.Emails.LastTokenTo(email);

        using var deactivation = await admin.PatchAsJsonAsync(
            $"/api/staff/{account.Id}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);
        deactivation.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var anonymous = factory.CreateClient();
        using var deactivatedLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, first!, NewPassword);
        deactivatedLink.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var inactiveInvitation = await StaffTestHelpers.ResendInvitationAsync(admin, account.Id);
        inactiveInvitation.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var reactivation = await admin.PatchAsJsonAsync(
            $"/api/staff/{account.Id}/status",
            new { isActive = true },
            TestContext.Current.CancellationToken);
        reactivation.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Reactivar no restaura el enlace que se invalidó al desactivar.
        using var oldAfterReactivation = await StaffTestHelpers.ResetPasswordAsync(anonymous, first!, NewPassword);
        oldAfterReactivation.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var resend = await StaffTestHelpers.ResendInvitationAsync(admin, account.Id);
        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = factory.Emails.LastTokenTo(email);

        using var newLink = await StaffTestHelpers.ResetPasswordAsync(anonymous, second!, NewPassword);
        newLink.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StaffTestHelpers.LoginAsync(anonymous, email, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_account_with_a_password_cannot_receive_an_invitation()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("invitacion.concluida");
        var accountId = await factory.SeedAccountAsync(email, NewPassword, StaffRole.Reception);

        using var response = await StaffTestHelpers.ResendInvitationAsync(admin, accountId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_invalid_link_is_rejected_without_internal_details()
    {
        using var anonymous = factory.CreateClient();

        using var response = await StaffTestHelpers.ResetPasswordAsync(
            anonymous,
            "enlace-que-nadie-emitio",
            NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("enlace-que-nadie-emitio");
        body.ShouldNotContain("Profesional");
    }

    private async Task<HttpClient> AdminAsync() =>
        await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);

    private static async Task<Guid> ProblemAccountIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("accountId").GetGuid();
    }
}
