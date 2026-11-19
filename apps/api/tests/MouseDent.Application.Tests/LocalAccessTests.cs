using MouseDent.Domain.Auth;
using MouseDent.Domain.Locales;
using Shouldly;

namespace MouseDent.Application.Tests;

/// <summary>
/// Regla de alcance por local que H-01 reutilizará: administración opera en todos
/// los locales, recepción solo en sus locales asignados y activos.
/// </summary>
public sealed class LocalAccessTests
{
    [Fact]
    public void An_administrator_operates_in_every_local_without_assignments()
    {
        var admin = StaffSamples.Account(StaffRole.Admin);
        var active = StaffSamples.NewLocal("Sede Centro");
        var inactive = StaffSamples.NewLocal("Sede Cerrada", isActive: false);

        LocalAccess.CanOperate(admin, active).ShouldBeTrue();
        LocalAccess.CanOperate(admin, inactive).ShouldBeTrue();
        LocalAccess.AccessibleLocals(admin, [active, inactive]).ShouldBe([active, inactive]);
    }

    [Fact]
    public void Reception_operates_only_in_its_active_assigned_locals()
    {
        var assigned = StaffSamples.NewLocal("Sede Centro");
        var other = StaffSamples.NewLocal("Sede Norte");
        var reception = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [assigned.Id]);

        LocalAccess.CanOperate(reception, assigned).ShouldBeTrue();
        LocalAccess.CanOperate(reception, other).ShouldBeFalse();
        LocalAccess.AccessibleLocals(reception, [assigned, other]).ShouldBe([assigned]);
    }

    [Fact]
    public void An_assignment_to_an_inactive_local_is_kept_but_does_not_authorize()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var reception = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [local.Id]);

        local.Deactivate();

        reception.AssignedLocalIds.ShouldBe([local.Id]);
        LocalAccess.CanOperate(reception, local).ShouldBeFalse();
        LocalAccess.AccessibleLocals(reception, [local]).ShouldBeEmpty();
    }

    [Fact]
    public void Reactivating_the_local_restores_the_access_of_its_assigned_account()
    {
        var local = StaffSamples.NewLocal("Sede Centro", isActive: false);
        var reception = StaffSamples.Account(StaffRole.Reception, assignedLocalIds: [local.Id]);

        local.Activate();

        LocalAccess.CanOperate(reception, local).ShouldBeTrue();
    }

    [Fact]
    public void A_professional_has_no_local_scope_in_this_delivery()
    {
        var local = StaffSamples.NewLocal("Sede Centro");
        var professional = StaffSamples.Account(StaffRole.Professional);

        LocalAccess.CanOperate(professional, local).ShouldBeFalse();
        LocalAccess.AccessibleLocals(professional, [local]).ShouldBeEmpty();
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

        LocalAccess.CanOperate(account, local).ShouldBeFalse();
    }
}
