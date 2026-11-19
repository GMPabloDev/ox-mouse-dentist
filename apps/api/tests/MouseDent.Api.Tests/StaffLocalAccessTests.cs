using System.Net;
using System.Net.Http.Json;
using MouseDent.Domain.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Comprueba la regla de alcance por local integrada con la base real: recepción
/// solo alcanza sus locales asignados y activos, y administración alcanza todos.
/// Los locales inactivos conservan la asignación sin autorizar operaciones.
/// </summary>
public sealed class StaffLocalAccessTests(MouseDentApiFactory factory) : IClassFixture<MouseDentApiFactory>
{
    [Fact]
    public async Task Reception_reaches_only_its_assigned_and_active_locals()
    {
        using var admin = await AdminAsync();
        var assigned = await StaffTestHelpers.CreateLocalAsync(admin);
        var other = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("acceso.recepcion");
        var accountId = await factory.SeedAccountAsync(email, "Recepcion123!", StaffRole.Reception);

        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            email,
            StaffRole.Reception,
            [assigned.Id]);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await factory.CanOperateAsync(accountId, assigned.Id)).ShouldBeTrue();
        (await factory.CanOperateAsync(accountId, other.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBe([assigned.Id]);
    }

    [Fact]
    public async Task Deactivating_the_local_removes_access_and_reactivating_it_restores_it()
    {
        using var admin = await AdminAsync();
        var local = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("acceso.local.estado");
        var accountId = await factory.SeedAccountAsync(email, "Recepcion123!", StaffRole.Reception);

        using var update = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            email,
            StaffRole.Reception,
            [local.Id]);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await factory.CanOperateAsync(accountId, local.Id)).ShouldBeTrue();

        await StaffTestHelpers.SetLocalStatusAsync(admin, local.Id, isActive: false);

        (await factory.CanOperateAsync(accountId, local.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBeEmpty();
        (await StaffTestHelpers.FindStaffAsync(admin, email)).AssignedLocalIds.ShouldBe([local.Id]);

        // Sin ningún local activo la cuenta sigue iniciando sesión, pero sin alcance.
        using var withoutLocals = factory.WithToken(await factory.LoginAsync(email, "Recepcion123!"));
        using var session = await withoutLocals.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        session.StatusCode.ShouldBe(HttpStatusCode.OK);

        await StaffTestHelpers.SetLocalStatusAsync(admin, local.Id, isActive: true);

        (await factory.CanOperateAsync(accountId, local.Id)).ShouldBeTrue();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBe([local.Id]);
    }

    [Fact]
    public async Task Removing_the_assignment_removes_the_local_scope_without_ending_the_session()
    {
        using var admin = await AdminAsync();
        var local = await StaffTestHelpers.CreateLocalAsync(admin);
        var email = StaffTestHelpers.UniqueEmail("acceso.retirado");
        var accountId = await factory.SeedAccountAsync(email, "Recepcion123!", StaffRole.Reception);

        using var assigned = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            email,
            StaffRole.Reception,
            [local.Id]);
        assigned.StatusCode.ShouldBe(HttpStatusCode.OK);

        var token = await factory.LoginAsync(email, "Recepcion123!");
        using var account = factory.WithToken(token);

        (await factory.CanOperateAsync(accountId, local.Id)).ShouldBeTrue();

        using var removed = await StaffTestHelpers.UpdateStaffAsync(
            admin,
            accountId,
            email,
            StaffRole.Reception,
            []);
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await factory.CanOperateAsync(accountId, local.Id)).ShouldBeFalse();
        (await factory.AccessibleLocalsAsync(accountId)).ShouldBeEmpty();

        using var session = await account.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        session.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Administration_reaches_every_local_by_profile()
    {
        using var admin = await AdminAsync();
        var active = await StaffTestHelpers.CreateLocalAsync(admin);
        var inactive = await StaffTestHelpers.CreateLocalAsync(admin, isActive: false);
        var seeded = await StaffTestHelpers.FindStaffAsync(admin, MouseDentApiFactory.AdminEmail);

        var reachable = await factory.AccessibleLocalsAsync(seeded.Id);

        reachable.ShouldContain(active.Id);
        reachable.ShouldContain(inactive.Id);
        seeded.AssignedLocalIds.ShouldBeEmpty();
    }

    private async Task<HttpClient> AdminAsync() =>
        await StaffTestHelpers.SignInAsync(
            factory,
            MouseDentApiFactory.AdminEmail,
            MouseDentApiFactory.AdminPassword);
}
