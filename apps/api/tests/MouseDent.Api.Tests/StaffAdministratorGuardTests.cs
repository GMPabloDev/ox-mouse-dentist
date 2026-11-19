using System.Net;
using System.Net.Http.Json;
using MouseDent.Api.Auth;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Protección del último administrador disponible contra la base real. Usa una
/// base en archivo para que dos solicitudes concurrentes abran conexiones
/// distintas y la comprobación y la escritura queden serializadas.
/// </summary>
public sealed class StaffAdministratorGuardTests
{
    private const string ExchangePassword = "Admin123!";

    [Fact]
    public async Task The_only_available_administrator_cannot_be_deactivated_demoted_or_reassigned_an_email()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        using var admin = await AdminAsync(factory);
        var seeded = await StaffTestHelpers.FindStaffAsync(admin, MouseDentApiFactory.AdminEmail);

        using var deactivation = await admin.PatchAsJsonAsync(
            $"/api/staff/{seeded.Id}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);

        using var demotion = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            seeded.Id,
            MouseDentApiFactory.AdminEmail,
            StaffRole.Professional);

        using var emailChange = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            seeded.Id,
            StaffTestHelpers.UniqueEmail("admin.nuevo"),
            StaffRole.Admin);

        foreach (var (response, operation) in new[]
                 {
                     (deactivation, "desactivar"),
                     (demotion, "cambiar el perfil"),
                     (emailChange, "cambiar el correo"),
                 })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict, operation);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        var stored = await StaffTestHelpers.FindStaffAsync(admin, MouseDentApiFactory.AdminEmail);
        stored.Status.ShouldBe(StaffAccountState.Activa);
        stored.Profile.ShouldBe(StaffRole.Admin);
    }

    [Fact]
    public async Task Two_concurrent_deactivations_leave_one_available_administrator()
    {
        using var factory = new MouseDentApiFileDatabaseFactory();
        var firstToken = await factory.AccessTokenAsync(MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
        using var firstClient = factory.WithToken(firstToken);

        var secondEmail = StaffTestHelpers.UniqueEmail("admin.concurrente");
        var secondId = await factory.SeedAccountAsync(secondEmail, ExchangePassword, StaffRole.Admin);
        var secondToken = await factory.LoginAsync(secondEmail, ExchangePassword);
        using var secondClient = factory.WithToken(secondToken);

        var first = await StaffTestHelpers.FindStaffAsync(firstClient, MouseDentApiFactory.AdminEmail);

        var firstRequest = firstClient.PatchAsJsonAsync(
            $"/api/staff/{first.Id}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);
        var secondRequest = secondClient.PatchAsJsonAsync(
            $"/api/staff/{secondId}/status",
            new { isActive = false },
            TestContext.Current.CancellationToken);

        await Task.WhenAll(firstRequest, secondRequest);

        using var firstResponse = firstRequest.Result;
        using var secondResponse = secondRequest.Result;
        var statuses = new[] { firstResponse.StatusCode, secondResponse.StatusCode };

        statuses.Count(status => status == HttpStatusCode.OK).ShouldBe(1);
        statuses.Count(status => status == HttpStatusCode.Conflict).ShouldBe(1);

        // El administrador que conserva el acceso sigue pudiendo consultar la lista.
        string? survivorToken = null;
        foreach (var token in new[] { firstToken, secondToken })
        {
            using var candidate = factory.WithToken(token);
            using var probe = await candidate.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

            if (probe.StatusCode == HttpStatusCode.OK)
            {
                survivorToken = token;
            }
        }

        survivorToken.ShouldNotBeNull();
        using var survivor = factory.WithToken(survivorToken!);
        var available = (await StaffTestHelpers.ListStaffAsync(survivor))
            .Where(account => account.Profile == StaffRole.Admin && account.Status == StaffAccountState.Activa)
            .ToArray();

        available.ShouldHaveSingleItem();
    }

    private static Task<HttpClient> AdminAsync(MouseDentApiFactoryBase factory) =>
        StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
}
