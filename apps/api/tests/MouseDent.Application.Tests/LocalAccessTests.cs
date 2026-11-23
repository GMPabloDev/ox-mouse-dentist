using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;
using Shouldly;

namespace MouseDent.Application.Tests;

/// <summary>
/// Regla de alcance por local que H-01 reutilizará: administración opera en todos
/// los locales, recepción solo en sus locales asignados y activos, y un
/// profesional solo en los locales de atención de su ficha activa, con la cuenta
/// activa y con contraseña establecida.
/// </summary>
public sealed class LocalAccessTests
{
    [Fact]
    public void An_administrator_operates_in_every_local_without_assignments_or_ficha()
    {
        var admin = StaffSamples.Account(StaffRole.Admin);
        var active = StaffSamples.NewLocal("Sede Centro");
        var inactive = StaffSamples.NewLocal("Sede Cerrada", isActive: false);

        LocalAccess.CanOperate(admin, ficha: null, active).ShouldBeTrue();
        LocalAccess.CanOperate(admin, ficha: null, inactive).ShouldBeTrue();
        LocalAccess.AccessibleLocals(admin, ficha: null, [active, inactive]).ShouldBe([active, inactive]);
    }

    [Fact]
    public void Reception_operates_only_in_its_active_assigned_locals()
    {
        var assigned = StaffSamples.NewLocal("Sede Centro");
        var other = StaffSamples.NewLocal("Sede Norte");
        var reception = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [assigned.Id]);

        LocalAccess.CanOperate(reception, ficha: null, assigned).ShouldBeTrue();
        LocalAccess.CanOperate(reception, ficha: null, other).ShouldBeFalse();
        LocalAccess.AccessibleLocals(reception, ficha: null, [assigned, other]).ShouldBe([assigned]);
    }

    [Fact]
    public void An_assignment_to_an_inactive_local_is_kept_but_does_not_authorize()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var reception = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [local.Id]);

        local.Deactivate();

        reception.AssignedLocalIds.ShouldBe([local.Id]);
        LocalAccess.CanOperate(reception, ficha: null, local).ShouldBeFalse();
        LocalAccess.AccessibleLocals(reception, ficha: null, [local]).ShouldBeEmpty();
    }

    [Fact]
    public void Reactivating_the_local_restores_the_access_of_its_assigned_account()
    {
        var local = StaffSamples.NewLocal("Sede Centro", isActive: false);
        var reception = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [local.Id]);

        local.Activate();

        LocalAccess.CanOperate(reception, ficha: null, local).ShouldBeTrue();
    }

    [Fact]
    public void A_professional_operates_only_in_the_active_locals_of_an_active_ficha()
    {
        var assigned = StaffSamples.NewLocal("Sede Centro");
        var other = StaffSamples.NewLocal("Sede Norte");
        var professional = StaffSamples.Account(StaffRole.Professional);
        var ficha = FichaDePrueba.Crear(professional.Id, assigned.Id);

        LocalAccess.CanOperate(professional, ficha.Ficha, assigned).ShouldBeTrue();
        LocalAccess.CanOperate(professional, ficha.Ficha, other).ShouldBeFalse();
        LocalAccess.AccessibleLocals(professional, ficha.Ficha, [assigned, other]).ShouldBe([assigned]);
    }

    [Fact]
    public void A_professional_without_a_ficha_operates_nowhere()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var professional = StaffSamples.Account(StaffRole.Professional);

        LocalAccess.CanOperate(professional, ficha: null, local).ShouldBeFalse();
        LocalAccess.AccessibleLocals(professional, ficha: null, [local]).ShouldBeEmpty();
    }

    [Fact]
    public void A_professional_with_an_inactive_ficha_operates_nowhere_and_reactivating_it_restores_the_access()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var professional = StaffSamples.Account(StaffRole.Professional);
        var ficha = FichaDePrueba.Crear(professional.Id, local.Id);

        ficha.Ficha.Desactivar();

        ficha.Ficha.LocalIds.ShouldBe([local.Id]);
        LocalAccess.CanOperate(professional, ficha.Ficha, local).ShouldBeFalse();
        LocalAccess.AccessibleLocals(professional, ficha.Ficha, [local]).ShouldBeEmpty();

        ficha.Ficha.Activar(ficha.Activos(local));

        LocalAccess.CanOperate(professional, ficha.Ficha, local).ShouldBeTrue();
        LocalAccess.AccessibleLocals(professional, ficha.Ficha, [local]).ShouldBe([local]);
    }

    [Theory]
    [InlineData("pendiente")]
    [InlineData("inactiva")]
    public void A_professional_with_a_pending_or_inactive_account_operates_nowhere(string estado)
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var professional = StaffSamples.Pending(StaffRole.Professional);
        var ficha = FichaDePrueba.Crear(professional.Id, local.Id);

        if (estado == "inactiva")
        {
            professional.Deactivate();
        }

        LocalAccess.CanOperate(professional, ficha.Ficha, local).ShouldBeFalse();
        LocalAccess.AccessibleLocals(professional, ficha.Ficha, [local]).ShouldBeEmpty();
    }

    [Fact]
    public void A_professional_assigned_to_an_inactive_local_does_not_operate_there()
    {
        var assigned = StaffSamples.NewLocal("Sede Centro");
        var other = StaffSamples.NewLocal("Sede Norte");
        var professional = StaffSamples.Account(StaffRole.Professional);
        var ficha = FichaDePrueba.Crear(professional.Id, assigned.Id, other.Id);

        assigned.Deactivate();

        ficha.Ficha.LocalIds.ShouldBe([assigned.Id, other.Id], ignoreOrder: true);
        LocalAccess.CanOperate(professional, ficha.Ficha, assigned).ShouldBeFalse();
        LocalAccess.CanOperate(professional, ficha.Ficha, other).ShouldBeTrue();
        LocalAccess.AccessibleLocals(professional, ficha.Ficha, [assigned, other]).ShouldBe([other]);

        assigned.Activate();

        LocalAccess.AccessibleLocals(professional, ficha.Ficha, [assigned, other])
            .ShouldBe([assigned, other], ignoreOrder: true);
    }

    [Fact]
    public void A_ficha_of_another_account_does_not_authorize()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var professional = StaffSamples.Account(StaffRole.Professional);
        var fichaAjena = FichaDePrueba.Crear(Guid.NewGuid(), local.Id);

        LocalAccess.CanOperate(professional, fichaAjena.Ficha, local).ShouldBeFalse();
        LocalAccess.AccessibleLocals(professional, fichaAjena.Ficha, [local]).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_account_without_a_valid_session_does_not_operate(bool deactivated)
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var account = deactivated
            ? StaffSamples.Account(StaffRole.Admin)
            : StaffSamples.Pending(StaffRole.Admin);

        if (deactivated)
        {
            account.Deactivate();
        }

        LocalAccess.CanOperate(account, ficha: null, local).ShouldBeFalse();
    }

    /// <summary>Ficha activa de prueba con su especialidad, para poder reactivarla con los activos.</summary>
    private sealed record FichaDePrueba(Profesional Ficha, Guid EspecialidadId)
    {
        public static FichaDePrueba Crear(Guid accountId, params Guid[] localIds)
        {
            var especialidadId = Guid.NewGuid();
            var ficha = Profesional.Crear(
                accountId,
                colegiatura: null,
                [especialidadId],
                localIds,
                new CatalogoActivo([especialidadId], localIds));

            return new FichaDePrueba(ficha, especialidadId);
        }

        public CatalogoActivo Activos(params Local[] locales) =>
            new([EspecialidadId], locales.Select(local => local.Id));
    }
}
