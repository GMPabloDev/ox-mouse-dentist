using System.Net;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Uso único del enlace bajo dos solicitudes simultáneas. Usa una base en archivo
/// para que cada solicitud abra su propia conexión y la escritura no comparta
/// contexto.
/// </summary>
public sealed class StaffAccessLinkSingleUseTests
{
    private const string FirstPassword = "Recepcion123!";
    private const string SecondPassword = "OtraClave123!";

    [Fact]
    public async Task Two_simultaneous_uses_of_the_same_link_set_the_password_once()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);

        var email = StaffTestHelpers.UniqueEmail("uso.unico");
        await StaffTestHelpers.CreatePendingStaffAsync(admin, email);
        var token = factory.Emails.LastTokenTo(email);
        token.ShouldNotBeNullOrWhiteSpace();

        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            StaffTestHelpers.ResetPasswordAsync(firstClient, token!, FirstPassword),
            StaffTestHelpers.ResetPasswordAsync(secondClient, token!, SecondPassword));

        responses.Count(response => response.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).ShouldBe(1);

        // Solo la contraseña del intento aceptado abre sesión.
        var accepted = responses[0].StatusCode == HttpStatusCode.NoContent ? FirstPassword : SecondPassword;
        var refused = accepted == FirstPassword ? SecondPassword : FirstPassword;

        using var anonymous = factory.CreateClient();
        (await StaffTestHelpers.LoginAsync(anonymous, email, accepted))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StaffTestHelpers.LoginAsync(anonymous, email, refused))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
