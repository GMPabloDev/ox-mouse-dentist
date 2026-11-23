using MouseDent.Domain.Catalogo;
using Shouldly;

namespace MouseDent.Application.Tests;

public sealed class CatalogoDomainTests
{
    [Fact]
    public void Creation_and_edit_trim_values_and_reject_invalid_data_without_changes()
    {
        var specialty = Especialidad.Create("  Odontología  ", "  Descripción  ");
        specialty.Name.ShouldBe("Odontología");
        specialty.Description.ShouldBe("Descripción");
        Should.Throw<ArgumentException>(() => specialty.Update(" ", null));
        Should.Throw<ArgumentException>(() => specialty.Update("Nombre", new string('x', 501)));
        specialty.Name.ShouldBe("Odontología");
    }

    [Fact]
    public void Service_creation_activation_and_moves_require_an_active_specialty()
    {
        var specialty = Especialidad.Create("Uno", null);
        var service = Servicio.Create("Limpieza", null, specialty);
        service.Deactivate();
        specialty.Deactivate();
        Should.Throw<InvalidOperationException>(() => service.Activate(specialty));
        Should.Throw<InvalidOperationException>(() => Servicio.Create("Otro", null, specialty));
        var destination = Especialidad.Create("Dos", null);
        service.MoveTo(destination);
        service.Activate(destination);
        service.EspecialidadId.ShouldBe(destination.Id);
        service.IsActive.ShouldBeTrue();
        specialty.Activate();
        specialty.IsActive.ShouldBeTrue();
    }
    [Fact]
    public void Limits_and_invalid_moves_preserve_the_original_values()
    {
        var specialty = Especialidad.Create(new string('a', 100), new string('b', 500));
        var service = Servicio.Create("Servicio", null, specialty);
        var inactive = Especialidad.Create("Inactiva", null);
        inactive.Deactivate();
        Should.Throw<ArgumentException>(() => service.Update(new string('a', 101), null));
        Should.Throw<InvalidOperationException>(() => service.MoveTo(inactive));
        service.Name.ShouldBe("Servicio");
        service.EspecialidadId.ShouldBe(specialty.Id);
        service.Deactivate();
        specialty.Deactivate();
        specialty.Activate();
        service.IsActive.ShouldBeFalse();
    }
}
