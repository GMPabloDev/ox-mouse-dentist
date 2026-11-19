using System.Net;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Vigencia de los enlaces de acceso. La prueba adelanta el reloj de la API: el
/// enlace sigue sirviendo a las 23 horas y se rechaza pasadas las 24.
/// </summary>
public sealed class StaffAccessLinkExpiryTests(MouseDentApiClockFactory factory)
    : IClassFixture<MouseDentApiClockFactory>
{
    private const string Password = "Recepcion123!";

    [Fact]
    public async Task A_link_works_before_its_lifetime_and_is_rejected_after_it()
    {
        using var admin = await AdminAsync();
        var earlyEmail = StaffTestHelpers.UniqueEmail("vigencia.temprana");
        var lateEmail = StaffTestHelpers.UniqueEmail("vigencia.tardia");
        await StaffTestHelpers.CreatePendingStaffAsync(admin, earlyEmail);
        await StaffTestHelpers.CreatePendingStaffAsync(admin, lateEmail);

        var early = factory.Emails.LastTokenTo(earlyEmail);
        var late = factory.Emails.LastTokenTo(lateEmail);

        using var anonymous = factory.CreateClient();

        factory.Clock.Advance(TimeSpan.FromHours(23));
        using var withinLifetime = await StaffTestHelpers.ResetPasswordAsync(anonymous, early!, Password);
        withinLifetime.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        factory.Clock.Advance(TimeSpan.FromHours(2));
        using var expired = await StaffTestHelpers.ResetPasswordAsync(anonymous, late!, Password);
        expired.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await StaffTestHelpers.LoginAsync(anonymous, lateEmail, Password))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> AdminAsync() =>
        await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
}
