using System.Net;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// La regla de acceso por local del profesional contra la base real: opera solo en
/// los locales de atención de su ficha activa, con la cuenta activa y con
/// contraseña establecida. Cambiar los locales, desactivar la ficha o el local se
/// aplica en la siguiente lectura, sin esperar al vencimiento del token.
/// </summary>
public sealed class ProfesionalLocalAccessTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    [Fact]
    public async Task A_professional_operates_only_in_the_active_assigned_locals_of_an_active_ficha_and_account()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var centro = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var norte = await ProfesionalTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("acceso.profesional");
        var ficha = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            email,
            [especialidad.Id],
            [centro.Id]);
        await ProfesionalTestHelpers.ActivateAccountAsync(factory, email);
        var accountId = ficha.StaffAccountId;

        (await factory.CanOperateAsync(accountId, centro.Id)).ShouldBeTrue();
        (await factory.CanOperateAsync(accountId, norte.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBe([centro.Id]);

        // Desactivar la ficha retira el alcance completo y la reactivación lo recupera.
        await SetFichaStatusAsync(admin, ficha.Id, isActive: false);

        (await factory.CanOperateAsync(accountId, centro.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBeEmpty();

        await SetFichaStatusAsync(admin, ficha.Id, isActive: true);

        (await factory.CanOperateAsync(accountId, centro.Id)).ShouldBeTrue();

        // Cambiar los locales de atención se aplica en la siguiente lectura.
        using (var editada = await ProfesionalTestHelpers.UpdateAsync(
            admin,
            ficha.Id,
            [especialidad.Id],
            [norte.Id]))
        {
            editada.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await factory.CanOperateAsync(accountId, centro.Id)).ShouldBeFalse();
        (await factory.CanOperateAsync(accountId, norte.Id)).ShouldBeTrue();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBe([norte.Id]);

        // Desactivar el local asignado lo conserva en la ficha pero no autoriza.
        await ProfesionalTestHelpers.SetLocalStatusAsync(admin, norte.Id, isActive: false);

        (await factory.CanOperateAsync(accountId, norte.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBeEmpty();
        (await ProfesionalTestHelpers.GetAsync(admin, ficha.Id)).Locales.ShouldHaveSingleItem().IsActive.ShouldBeFalse();

        await ProfesionalTestHelpers.SetLocalStatusAsync(admin, norte.Id, isActive: true);

        (await factory.CanOperateAsync(accountId, norte.Id)).ShouldBeTrue();

        // Desactivar la cuenta tampoco toca la ficha, pero deja de autorizar.
        await ProfesionalTestHelpers.SetAccountStatusAsync(admin, accountId, isActive: false);

        (await factory.CanOperateAsync(accountId, norte.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBeEmpty();
        (await ProfesionalTestHelpers.GetAsync(admin, ficha.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_pending_account_or_an_account_without_a_ficha_does_not_operate()
    {
        using var admin = await AdminAsync();
        var especialidad = await ProfesionalTestHelpers.CreateEspecialidadAsync(admin);
        var local = await ProfesionalTestHelpers.CreateLocalAsync(admin);

        // Ficha creada con la invitación todavía pendiente.
        var pendiente = await ProfesionalTestHelpers.CreateProfesionalAsync(
            admin,
            StaffTestHelpers.UniqueEmail("acceso.pendiente"),
            [especialidad.Id],
            [local.Id]);

        (await factory.CanOperateAsync(pendiente.StaffAccountId, local.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(pendiente.StaffAccountId)).ShouldBeEmpty();

        // Cuenta Profesional con contraseña y sin ficha: se vincula por correo al alta.
        var sinFichaEmail = StaffTestHelpers.UniqueEmail("acceso.sin.ficha");
        var sinFicha = await factory.SeedAccountAsync(
            sinFichaEmail,
            ProfesionalTestHelpers.NewPassword,
            StaffRole.Professional);

        (await factory.CanOperateAsync(sinFicha, local.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(sinFicha)).ShouldBeEmpty();
    }

    private Task<HttpClient> AdminAsync() =>
        StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);

    private static async Task SetFichaStatusAsync(HttpClient admin, Guid id, bool isActive)
    {
        using var response = await ProfesionalTestHelpers.SetFichaStatusAsync(admin, id, isActive);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
