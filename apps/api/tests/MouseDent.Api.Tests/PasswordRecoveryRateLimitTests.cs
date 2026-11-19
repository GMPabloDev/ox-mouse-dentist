using System.Net;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Límite de las solicitudes públicas de recuperación. La prueba usa una API con
/// el límite configurado en tres solicitudes por ventana.
/// </summary>
public sealed class PasswordRecoveryRateLimitTests(MouseDentApiRecoveryLimitFactory factory)
    : IClassFixture<MouseDentApiRecoveryLimitFactory>
{
    [Fact]
    public async Task The_configured_limit_is_enforced_without_revealing_whether_the_account_exists()
    {
        var known = StaffTestHelpers.UniqueEmail("limite.conocida");
        await factory.SeedAccountAsync(known, MouseDentApiFactory.ReceptionPassword, StaffRole.Reception);
        var unknown = StaffTestHelpers.UniqueEmail("limite.desconocida");
        var limit = MouseDentApiRecoveryLimitFactory.RecoveryPermitLimit;

        using var anonymous = factory.CreateClient();
        var answers = new List<string>();

        foreach (var address in new[] { known, unknown, known })
        {
            using var response = await StaffTestHelpers.RequestRecoveryAsync(anonymous, address);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted, address);
            answers.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        // La respuesta es idéntica exista o no la cuenta.
        answers.Distinct().Count().ShouldBe(1);

        using var blocked = await StaffTestHelpers.RequestRecoveryAsync(
            anonymous,
            StaffTestHelpers.UniqueEmail("limite.bloqueada"));
        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        ((int)blocked.StatusCode).ShouldBe(429);
        limit.ShouldBe(3);
    }
}
