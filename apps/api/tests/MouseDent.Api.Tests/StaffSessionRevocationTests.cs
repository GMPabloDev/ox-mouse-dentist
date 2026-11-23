using System.Net;
using System.Net.Http.Json;
using MouseDent.Api.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Un token ya emitido deja de servir en la siguiente solicitud cuando la cuenta
/// se desactiva, cambia de perfil o cambia de correo. No se espera al vencimiento
/// del token.
/// </summary>
public sealed class StaffSessionRevocationTests(MouseDentApiFactory factory)
    : IClassFixture<MouseDentApiFactory>
{
    private const string Password = "Recepcion123!";

    [Fact]
    public async Task A_deactivated_account_cannot_use_an_already_issued_token()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("sesion.desactivada");
        var accountId = await factory.SeedAccountAsync(email, Password, StaffRole.Reception);
        var token = await factory.LoginAsync(email, Password);
        using var account = factory.WithToken(token);

        using var before = await account.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var deactivated = await admin.PatchAsJsonAsync(
            $"/api/staff/{accountId}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);
        deactivated.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var after = await account.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var anonymous = factory.CreateClient();
        using var login = await anonymous.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = Password },
            TestContext.Current.CancellationToken);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var reactivated = await admin.PatchAsJsonAsync(
            $"/api/staff/{accountId}/status",
            new { isActive = true },
            TestContext.Current.CancellationToken);
        reactivated.StatusCode.ShouldBe(HttpStatusCode.OK);

        // La sesión anterior no revive; la contraseña ya establecida sí permite una nueva.
        using var previous = await account.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        previous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var renewed = factory.WithToken(await factory.LoginAsync(email, Password));
        using var current = await renewed.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_the_profile_invalidates_the_previous_session()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("sesion.perfil");
        var accountId = await factory.SeedAccountAsync(email, Password, StaffRole.Reception);
        var token = await factory.LoginAsync(email, Password);
        using var account = factory.WithToken(token);

        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            email,
            StaffRole.Admin);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StaffTestHelpers.ReadStaffAsync(update)).Profile.ShouldBe(StaffRole.Admin);

        using var after = await account.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var renewed = factory.WithToken(await factory.LoginAsync(email, Password));
        using var current = await renewed.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_the_email_invalidates_the_previous_session_and_leaves_the_account_pending()
    {
        using var admin = await AdminAsync();
        var email = StaffTestHelpers.UniqueEmail("sesion.correo");
        var newEmail = StaffTestHelpers.UniqueEmail("sesion.correo.nuevo");
        var accountId = await factory.SeedAccountAsync(email, Password, StaffRole.Reception);
        var token = await factory.LoginAsync(email, Password);
        using var account = factory.WithToken(token);

        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            newEmail,
            StaffRole.Reception);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await StaffTestHelpers.ReadStaffAsync(update);
        updated.Email.ShouldBe(newEmail);
        updated.Status.ShouldBe(StaffAccountState.Pendiente);

        using var after = await account.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var anonymous = factory.CreateClient();
        foreach (var (candidate, password) in new[] { (newEmail, Password), (email, Password) })
        {
            using var login = await anonymous.PostAsJsonAsync(
                "/api/auth/login",
                new { email = candidate, password },
                TestContext.Current.CancellationToken);
            login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, candidate);
        }
    }

    private async Task<HttpClient> AdminAsync() =>
        await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
}
