using MouseDent.Application.Auth;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;
using Shouldly;

namespace MouseDent.Application.Tests;

/// <summary>
/// El servicio de alcance por local resuelve la regla con el estado persistido y
/// lee la ficha en cada llamada: cambiar los locales de atención, desactivar la
/// ficha o reactivarla se aplica en la siguiente solicitud, sin esperar al
/// vencimiento del token.
/// </summary>
public sealed class LocalAccessServiceTests
{
    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_professional_operates_only_in_the_active_locals_of_an_active_ficha()
    {
        var contexto = Contexto.Crear();
        var service = contexto.Service();

        (await service.CanOperateAsync(contexto.Account.Id, contexto.Local.Id, Token)).ShouldBeTrue();
        (await service.CanOperateAsync(contexto.Account.Id, contexto.OtroLocal.Id, Token)).ShouldBeFalse();
        (await service.AccessibleLocalsAsync(contexto.Account.Id, Token))
            .Select(local => local.Id)
            .ShouldBe([contexto.Local.Id]);
    }

    [Fact]
    public async Task The_next_call_reflects_the_locals_changed_between_calls_without_caching_the_ficha()
    {
        var contexto = Contexto.Crear();
        var service = contexto.Service();

        (await service.CanOperateAsync(contexto.Account.Id, contexto.Local.Id, Token)).ShouldBeTrue();

        contexto.Ficha.Editar(
            colegiatura: null,
            [contexto.EspecialidadId],
            [contexto.OtroLocal.Id],
            contexto.Activos());

        (await service.CanOperateAsync(contexto.Account.Id, contexto.Local.Id, Token)).ShouldBeFalse();
        (await service.CanOperateAsync(contexto.Account.Id, contexto.OtroLocal.Id, Token)).ShouldBeTrue();
        (await service.AccessibleLocalsAsync(contexto.Account.Id, Token))
            .Select(local => local.Id)
            .ShouldBe([contexto.OtroLocal.Id]);
    }

    [Fact]
    public async Task The_next_call_reflects_the_deactivation_and_the_reactivation_of_the_ficha()
    {
        var contexto = Contexto.Crear();
        var service = contexto.Service();

        contexto.Ficha.Desactivar();

        (await service.CanOperateAsync(contexto.Account.Id, contexto.Local.Id, Token)).ShouldBeFalse();
        (await service.AccessibleLocalsAsync(contexto.Account.Id, Token)).ShouldBeEmpty();

        contexto.Ficha.Activar(contexto.Activos());

        (await service.CanOperateAsync(contexto.Account.Id, contexto.Local.Id, Token)).ShouldBeTrue();
        (await service.AccessibleLocalsAsync(contexto.Account.Id, Token))
            .Select(local => local.Id)
            .ShouldBe([contexto.Local.Id]);
    }

    [Fact]
    public async Task The_next_call_reflects_the_deactivation_of_the_assigned_local()
    {
        var contexto = Contexto.Crear();
        var service = contexto.Service();

        contexto.Local.Deactivate();

        contexto.Ficha.LocalIds.ShouldContain(contexto.Local.Id);
        (await service.CanOperateAsync(contexto.Account.Id, contexto.Local.Id, Token)).ShouldBeFalse();
        (await service.AccessibleLocalsAsync(contexto.Account.Id, Token)).ShouldBeEmpty();

        contexto.Local.Activate();

        (await service.CanOperateAsync(contexto.Account.Id, contexto.Local.Id, Token)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_professional_without_a_ficha_operates_nowhere()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Account(StaffRole.Professional);
        var service = new LocalAccessService(
            new FakeStaffAccounts(account),
            new FakeProfesionales(),
            new FakeLocalStore(local));

        (await service.CanOperateAsync(account.Id, local.Id, Token)).ShouldBeFalse();
        (await service.AccessibleLocalsAsync(account.Id, Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_pending_professional_account_operates_nowhere_even_with_an_active_ficha()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = StaffSamples.Pending(StaffRole.Professional);
        var especialidadId = Guid.NewGuid();
        var ficha = Profesional.Crear(
            account.Id,
            colegiatura: null,
            [especialidadId],
            [local.Id],
            new CatalogoActivo([especialidadId], [local.Id]));

        // La ficha existe pero la cuenta todavía no tiene contraseña establecida.
        var service = new LocalAccessService(
            new FakeStaffAccounts(account),
            new FakeProfesionales(ficha),
            new FakeLocalStore(local));

        (await service.CanOperateAsync(account.Id, local.Id, Token)).ShouldBeFalse();
        (await service.AccessibleLocalsAsync(account.Id, Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Administration_and_reception_keep_their_rule_without_a_ficha()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var otro = StaffSamples.NewLocal("Sede Norte");
        var admin = StaffSamples.Account(StaffRole.Admin);
        var reception = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [local.Id]);
        var service = new LocalAccessService(
            new FakeStaffAccounts(admin, reception),
            new FakeProfesionales(),
            new FakeLocalStore(local, otro));

        (await service.CanOperateAsync(admin.Id, otro.Id, Token)).ShouldBeTrue();
        (await service.AccessibleLocalsAsync(admin.Id, Token)).Count.ShouldBe(2);
        (await service.CanOperateAsync(reception.Id, local.Id, Token)).ShouldBeTrue();
        (await service.CanOperateAsync(reception.Id, otro.Id, Token)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_or_missing_local_does_not_authorize()
    {
        var contexto = Contexto.Crear();
        var service = contexto.Service();

        (await service.CanOperateAsync(Guid.NewGuid(), contexto.Local.Id, Token)).ShouldBeFalse();
        (await service.CanOperateAsync(contexto.Account.Id, Guid.NewGuid(), Token)).ShouldBeFalse();
        (await service.AccessibleLocalsAsync(Guid.NewGuid(), Token)).ShouldBeEmpty();
    }

    /// <summary>Cuenta profesional con contraseña establecida, ficha activa y dos locales.</summary>
    private sealed record Contexto(
        StaffAccount Account,
        Guid EspecialidadId,
        Profesional Ficha,
        Local Local,
        Local OtroLocal)
    {
        public static Contexto Crear()
        {
            var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
            var local = StaffSamples.NewLocal("Sede Centro");
            var otro = StaffSamples.NewLocal("Sede Norte");
            var account = StaffSamples.Account(StaffRole.Professional);
            var ficha = Profesional.Crear(
                account.Id,
                colegiatura: null,
                [especialidad.Id],
                [local.Id],
                new CatalogoActivo([especialidad.Id], [local.Id, otro.Id]));

            return new Contexto(account, especialidad.Id, ficha, local, otro);
        }

        public CatalogoActivo Activos() =>
            new([EspecialidadId], [Local.Id, OtroLocal.Id]);

        public LocalAccessService Service() =>
            new(
                new FakeStaffAccounts(Account),
                new FakeProfesionales(Ficha),
                new FakeLocalStore(Local, OtroLocal));
    }
}
