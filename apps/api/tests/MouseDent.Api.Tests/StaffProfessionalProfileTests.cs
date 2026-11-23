using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MouseDent.Api.Auth;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;
using MouseDent.Domain.Locales;
using MouseDent.Infrastructure.Persistence;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// El perfil <c>Profesional</c> queda fuera del módulo de personal: no se crea ni se
/// asigna desde la API de cuentas, y el perfil de una cuenta con ficha no se cambia
/// ahí. Sus datos de contacto se siguen corrigiendo en personal, porque la ficha no
/// los duplica. Las cuentas Profesional existentes conservan su acceso.
/// </summary>
public sealed class StaffProfessionalProfileTests(MouseDentApiFactory factory)
    : IClassFixture<MouseDentApiFactory>
{
    private const string Password = "Profesional123!";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Changing_the_profile_of_an_account_with_a_ficha_is_rejected()
    {
        var registration = await RegisterProfessionalAsync();
        using var admin = await AdminAsync();
        var account = await StaffTestHelpers.FindStaffAsync(admin, registration.Email);
        account.Profile.ShouldBe(StaffRole.Professional);

        using var response = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            account.Id,
            account.Email,
            StaffRole.Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(Token);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Perfil vinculado a una ficha de profesional");
        problem.Detail.ShouldNotBeNullOrWhiteSpace();

        // La cuenta y su ficha quedan como estaban.
        var stored = await StaffTestHelpers.FindStaffAsync(admin, account.Email);
        stored.Profile.ShouldBe(StaffRole.Professional);
        stored.DisplayName.ShouldBe(account.DisplayName);

        var summary = (await ListAsync(account.Email)).ShouldHaveSingleItem();
        summary.Id.ShouldBe(registration.ProfesionalId);
        summary.AccountStatus.ShouldBe(EstadoDeCuenta.Pendiente);
    }

    [Fact]
    public async Task Editing_the_contact_data_of_a_linked_account_keeps_the_profile_and_the_ficha_shows_it()
    {
        var registration = await RegisterProfessionalAsync();
        using var admin = await AdminAsync();
        var account = await StaffTestHelpers.FindStaffAsync(admin, registration.Email);
        var newEmail = StaffTestHelpers.UniqueEmail("profesional.corregido");

        using var response = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            account.Id,
            newEmail,
            StaffRole.Professional,
            displayName: "Nombre Corregido",
            phone: "+51 999 111 222");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await StaffTestHelpers.ReadStaffAsync(response);
        updated.Profile.ShouldBe(StaffRole.Professional);
        updated.DisplayName.ShouldBe("Nombre Corregido");
        updated.Email.ShouldBe(newEmail);
        updated.Phone.ShouldBe("+51 999 111 222");
        updated.Status.ShouldBe(StaffAccountState.Pendiente);

        // La ficha no guarda esos datos: los lee de la cuenta y los muestra al día.
        var summary = (await ListAsync(newEmail)).ShouldHaveSingleItem();
        summary.Id.ShouldBe(registration.ProfesionalId);
        summary.StaffAccountId.ShouldBe(account.Id);
        summary.DisplayName.ShouldBe("Nombre Corregido");
        summary.Email.ShouldBe(newEmail);
        summary.Phone.ShouldBe("+51 999 111 222");
        summary.Colegiatura.ShouldBe(registration.Colegiatura);
    }

    [Fact]
    public async Task A_professional_account_without_a_ficha_can_change_its_profile()
    {
        var email = StaffTestHelpers.UniqueEmail("profesional.sin.ficha");
        var accountId = await factory.SeedAccountAsync(email, Password, StaffRole.Professional);
        using var admin = await AdminAsync();

        using var response = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            email,
            StaffRole.Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StaffTestHelpers.ReadStaffAsync(response)).Profile.ShouldBe(StaffRole.Admin);
    }

    [Fact]
    public async Task An_existing_professional_account_keeps_signing_in()
    {
        var email = StaffTestHelpers.UniqueEmail("profesional.existente");
        await factory.SeedAccountAsync(email, Password, StaffRole.Professional);

        using var anonymous = factory.CreateClient();
        using var login = await StaffTestHelpers.LoginAsync(anonymous, email, Password);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var token = await factory.AccessTokenAsync(email, Password);
        using var session = factory.WithToken(token);
        using var me = await session.GetAsync("/api/auth/me", Token);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<RegisteredProfessional> RegisterProfessionalAsync()
    {
        var especialidadId = await SeedEspecialidadAsync();
        var localId = await SeedLocalAsync();
        var email = StaffTestHelpers.UniqueEmail("profesional.alta");
        var colegiatura = $"COP {Guid.NewGuid():N}"[..16];

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProfesionalManagementService>();
        var outcome = await service.RegisterAsync(
            new ProfesionalAltaCommand(
                "Profesional de pruebas",
                email,
                colegiatura,
                [especialidadId],
                [localId]),
            Token);

        outcome.Status.ShouldBe(ProfesionalAltaStatus.Saved);

        return new RegisteredProfessional(outcome.Ficha!.Id, email, colegiatura);
    }

    private async Task<IReadOnlyList<ProfesionalResumen>> ListAsync(string search)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProfesionalManagementService>();

        return await service.ListAsync(new ProfesionalFiltro { Search = search }, Token);
    }

    private async Task<Guid> SeedEspecialidadAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var especialidad = Especialidad.Create($"Especialidad {Guid.NewGuid():N}", null);

        database.Especialidades.Add(especialidad);
        await database.SaveChangesAsync(Token);

        return especialidad.Id;
    }

    private async Task<Guid> SeedLocalAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MouseDentDbContext>();
        var schedule = WeeklySchedule.Parse(WeeklySchedule.WeekDays.Select(day =>
            new ScheduleDayInput(day, false, new TimeOnly(9, 0), new TimeOnly(18, 0)))).Schedule!;
        var local = Local.Create(
            $"Sede {Guid.NewGuid():N}",
            "Av. Siempre Viva 742",
            "+51 999 888 777",
            schedule);

        database.Locales.Add(local);
        await database.SaveChangesAsync(Token);

        return local.Id;
    }

    private Task<HttpClient> AdminAsync() =>
        StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);

    private sealed record RegisteredProfessional(Guid ProfesionalId, string Email, string Colegiatura);

    private sealed record ProblemPayload(string? Title, string? Detail);
}
