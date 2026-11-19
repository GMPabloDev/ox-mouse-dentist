using System.Net;
using System.Net.Http.Json;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Recuperación desde la pantalla de acceso. La respuesta no confirma si la
/// cuenta existe, el enlace sirve una vez y solo completar la contraseña
/// invalida las sesiones anteriores.
/// </summary>
public sealed class StaffPasswordRecoveryTests(MouseDentApiFactory factory)
    : IClassFixture<MouseDentApiFactory>
{
    private const string Password = "Recepcion123!";
    private const string NewPassword = "NuevaClave123!";

    [Fact]
    public async Task A_recovery_request_answers_the_same_for_a_known_and_an_unknown_address()
    {
        var email = StaffTestHelpers.UniqueEmail("recuperacion.conocida");
        await factory.SeedAccountAsync(email, Password, StaffRole.Reception);
        var unknown = StaffTestHelpers.UniqueEmail("recuperacion.desconocida");

        using var anonymous = factory.CreateClient();
        using var known = await StaffTestHelpers.RequestRecoveryAsync(anonymous, email);
        using var missing = await StaffTestHelpers.RequestRecoveryAsync(anonymous, unknown);

        known.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        missing.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(await known.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        factory.Emails.SentTo(email).ShouldHaveSingleItem();
        factory.Emails.SentTo(unknown).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_recovery_link_sets_a_new_password_and_revokes_the_previous_sessions()
    {
        var email = StaffTestHelpers.UniqueEmail("recuperacion.sesion");
        await factory.SeedAccountAsync(email, Password, StaffRole.Reception);
        var sessionToken = await factory.LoginAsync(email, Password);
        using var session = factory.WithToken(sessionToken);

        using var anonymous = factory.CreateClient();
        using var request = await StaffTestHelpers.RequestRecoveryAsync(anonymous, email);
        request.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Solicitar el enlace no cierra la sesión abierta.
        using var stillValid = await session.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        stillValid.StatusCode.ShouldBe(HttpStatusCode.OK);

        var token = factory.Emails.LastTokenTo(email);
        token.ShouldNotBeNullOrWhiteSpace();

        using var completion = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, NewPassword);
        completion.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var revoked = await session.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        revoked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await StaffTestHelpers.LoginAsync(anonymous, email, Password))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await StaffTestHelpers.LoginAsync(anonymous, email, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        using var replay = await StaffTestHelpers.ResetPasswordAsync(anonymous, token!, "OtraClave123!");
        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_recovery_link_replaces_the_pending_invitation()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("recuperacion.invitacion");
        await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var invitation = factory.Emails.LastTokenTo(email);

        using var anonymous = factory.CreateClient();
        using var request = await StaffTestHelpers.RequestRecoveryAsync(anonymous, email);
        request.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var recovery = factory.Emails.LastTokenTo(email);

        recovery.ShouldNotBe(invitation);

        using var previous = await StaffTestHelpers.ResetPasswordAsync(anonymous, invitation!, NewPassword);
        previous.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var current = await StaffTestHelpers.ResetPasswordAsync(anonymous, recovery!, NewPassword);
        current.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await StaffTestHelpers.LoginAsync(anonymous, email, NewPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task No_recovery_link_is_sent_to_an_inactive_account()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("recuperacion.inactiva");
        var accountId = await factory.SeedAccountAsync(email, Password, StaffRole.Reception);

        using var deactivation = await admin.PatchAsJsonAsync(
            $"/api/staff/{accountId}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);
        deactivation.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var anonymous = factory.CreateClient();
        using var request = await StaffTestHelpers.RequestRecoveryAsync(anonymous, email);
        request.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        factory.Emails.SentTo(email).ShouldBeEmpty();

        // Sin enlace, la contraseña anterior tampoco abre una sesión.
        (await StaffTestHelpers.LoginAsync(anonymous, email, Password))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> AdminAsync() =>
        await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
}
