using System.Net;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// La regla de acceso por local del profesional contra la base real: opera solo en
/// los locales de atención de su ficha activa, con la cuenta activa y con
/// contraseña establecida. Cambiar los locales, desactivar la ficha, el local o la
/// cuenta se aplica en la siguiente lectura, sin esperar al vencimiento del token.
/// </summary>
public sealed class ProfesionalLocalAccessTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    [Fact]
    public async Task A_professional_operates_only_in_the_active_assigned_locals()
    {
        using var acceso = await AccessAsync();

        (await factory.CanOperateAsync(acceso.AccountId, acceso.CentroId)).ShouldBeTrue();
        (await factory.CanOperateAsync(acceso.AccountId, acceso.NorteId)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(acceso.AccountId)).ShouldBe([acceso.CentroId]);
    }

    [Fact]
    public async Task Deactivating_the_ficha_removes_the_access_and_reactivating_it_restores_it()
    {
        using var acceso = await AccessAsync();

        await SetFichaStatusAsync(acceso, isActive: false);

        (await factory.CanOperateAsync(acceso.AccountId, acceso.CentroId)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(acceso.AccountId)).ShouldBeEmpty();

        await SetFichaStatusAsync(acceso, isActive: true);

        (await factory.CanOperateAsync(acceso.AccountId, acceso.CentroId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Changing_the_locals_of_the_ficha_applies_on_the_next_request()
    {
        using var acceso = await AccessAsync();

        using (var editada = await ProfesionalTestHelpers.UpdateAsync(
            acceso.Admin,
            acceso.FichaId,
            [acceso.EspecialidadId],
            [acceso.NorteId]))
        {
            editada.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await factory.CanOperateAsync(acceso.AccountId, acceso.CentroId)).ShouldBeFalse();
        (await factory.CanOperateAsync(acceso.AccountId, acceso.NorteId)).ShouldBeTrue();
        (await factory.AccessibleLocalsAsync(acceso.AccountId)).ShouldBe([acceso.NorteId]);
    }

    [Fact]
    public async Task Deactivating_the_assigned_local_keeps_it_in_the_ficha_without_access()
    {
        using var acceso = await AccessAsync();

        await ProfesionalTestHelpers.SetLocalStatusAsync(acceso.Admin, acceso.CentroId, isActive: false);

        (await factory.CanOperateAsync(acceso.AccountId, acceso.CentroId)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(acceso.AccountId)).ShouldBeEmpty();
        (await ProfesionalTestHelpers.GetAsync(acceso.Admin, acceso.FichaId))
            .Locales.ShouldHaveSingleItem()
            .IsActive.ShouldBeFalse();

        await ProfesionalTestHelpers.SetLocalStatusAsync(acceso.Admin, acceso.CentroId, isActive: true);

        (await factory.CanOperateAsync(acceso.AccountId, acceso.CentroId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivating_the_account_removes_the_access_without_touching_the_ficha()
    {
        using var acceso = await AccessAsync();

        await ProfesionalTestHelpers.SetAccountStatusAsync(acceso.Admin, acceso.AccountId, isActive: false);

        (await factory.CanOperateAsync(acceso.AccountId, acceso.CentroId)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(acceso.AccountId)).ShouldBeEmpty();
        (await ProfesionalTestHelpers.GetAsync(acceso.Admin, acceso.FichaId)).IsActive.ShouldBeTrue();
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

    /// <summary>Administración, una especialidad, dos locales y una cuenta activa.</summary>
    private sealed record Acceso(
        HttpClient Admin,
        Guid AccountId,
        Guid FichaId,
        Guid EspecialidadId,
        Guid CentroId,
        Guid NorteId) : IDisposable
    {
        public void Dispose() => Admin.Dispose();
    }

    /// <summary>Ficha activa asignada a la sede centro, con la cuenta ya activada.</summary>
    private async Task<Acceso> AccessAsync()
    {
        var admin = await AdminAsync();
        try
        {
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

            return new Acceso(admin, ficha.StaffAccountId, ficha.Id, especialidad.Id, centro.Id, norte.Id);
        }
        catch
        {
            admin.Dispose();
            throw;
        }
    }

    private async Task SetFichaStatusAsync(Acceso acceso, bool isActive)
    {
        using var response = await ProfesionalTestHelpers.SetFichaStatusAsync(acceso.Admin, acceso.FichaId, isActive);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private Task<HttpClient> AdminAsync() =>
        StaffTestHelpers.SignInAsync(factory, MouseDentApiFactory.AdminEmail, MouseDentApiFactory.AdminPassword);
}
