using MouseDent.Domain.Profesionales;
using Shouldly;

namespace MouseDent.Application.Tests;

/// <summary>
/// Reglas de la ficha del profesional. La ficha solo conoce los identificadores
/// asignados, así que cada prueba entrega también qué especialidades y locales
/// están activos, igual que hará la aplicación con el catálogo y con los locales.
/// </summary>
public sealed class ProfesionalesDomainTests
{
    [Fact]
    public void Creation_links_the_account_and_keeps_the_visible_and_normalized_colegiatura()
    {
        var accountId = Guid.NewGuid();
        var odontologia = Guid.NewGuid();
        var ortodoncia = Guid.NewGuid();
        var sedeNorte = Guid.NewGuid();
        var sedeSur = Guid.NewGuid();

        var ficha = Profesional.Crear(
            accountId,
            "  COP 12345  ",
            [ortodoncia, odontologia, ortodoncia],
            [sedeSur, sedeNorte],
            Activos([odontologia, ortodoncia], [sedeNorte, sedeSur]));

        ficha.Id.ShouldNotBe(Guid.Empty);
        ficha.StaffAccountId.ShouldBe(accountId);
        ficha.IsActive.ShouldBeTrue();
        ficha.Colegiatura.ShouldBe("COP 12345");
        ficha.ColegiaturaNormalizada.ShouldBe("COP12345");
        ficha.EspecialidadIds.ShouldBe([odontologia, ortodoncia], ignoreOrder: true);
        ficha.EspecialidadIds.Count.ShouldBe(2);
        ficha.LocalIds.ShouldBe([sedeNorte, sedeSur], ignoreOrder: true);
        ficha.LocalIds.Count.ShouldBe(2);
    }

    [Fact]
    public void Creation_requires_an_account()
    {
        var especialidad = Guid.NewGuid();
        var local = Guid.NewGuid();

        Should.Throw<ArgumentException>(() => Profesional.Crear(
            Guid.Empty,
            null,
            [especialidad],
            [local],
            Activos([especialidad], [local])));
    }

    [Fact]
    public void Creation_rejects_inactive_specialties_and_locales()
    {
        var activa = Guid.NewGuid();
        var inactiva = Guid.NewGuid();
        var localActivo = Guid.NewGuid();
        var localInactivo = Guid.NewGuid();
        var activos = Activos([activa], [localActivo]);

        Should.Throw<InvalidOperationException>(() =>
            Profesional.Crear(Guid.NewGuid(), null, [activa, inactiva], [localActivo], activos));
        Should.Throw<InvalidOperationException>(() =>
            Profesional.Crear(Guid.NewGuid(), null, [activa], [localActivo, localInactivo], activos));
    }

    [Fact]
    public void Creation_requires_at_least_one_active_specialty_and_one_active_local()
    {
        var activa = Guid.NewGuid();
        var inactiva = Guid.NewGuid();
        var localActivo = Guid.NewGuid();
        var localInactivo = Guid.NewGuid();
        var activos = Activos([activa], [localActivo]);

        Should.Throw<InvalidOperationException>(() =>
            Profesional.Crear(Guid.NewGuid(), null, [], [localActivo], activos));
        Should.Throw<InvalidOperationException>(() =>
            Profesional.Crear(Guid.NewGuid(), null, [inactiva], [localActivo], activos));
        Should.Throw<InvalidOperationException>(() =>
            Profesional.Crear(Guid.NewGuid(), null, [activa], [], activos));
        Should.Throw<InvalidOperationException>(() =>
            Profesional.Crear(Guid.NewGuid(), null, [activa], [localInactivo], activos));

        Profesional.Crear(Guid.NewGuid(), null, [activa], [localActivo], activos).IsActive.ShouldBeTrue();
    }

    [Fact]
    public void The_colegiatura_is_optional_accepts_thirty_characters_and_ignores_case_and_spaces()
    {
        var especialidad = Guid.NewGuid();
        var local = Guid.NewGuid();
        var activos = Activos([especialidad], [local]);

        Profesional.Crear(Guid.NewGuid(), " cop 12345 ", [especialidad], [local], activos)
            .ColegiaturaNormalizada.ShouldBe("COP12345");
        Profesional.Crear(Guid.NewGuid(), "COP12345", [especialidad], [local], activos)
            .ColegiaturaNormalizada.ShouldBe("COP12345");
        Profesional.Crear(Guid.NewGuid(), null, [especialidad], [local], activos)
            .Colegiatura.ShouldBeNull();
        Profesional.Crear(Guid.NewGuid(), "   ", [especialidad], [local], activos)
            .Colegiatura.ShouldBeNull();

        var treinta = new string('9', 30);
        var conLimite = Profesional.Crear(Guid.NewGuid(), $" {treinta} ", [especialidad], [local], activos);
        conLimite.Colegiatura.ShouldBe(treinta);
        conLimite.ColegiaturaNormalizada.ShouldBe(treinta);

        Should.Throw<ArgumentException>(() =>
            Profesional.Crear(Guid.NewGuid(), new string('9', 31), [especialidad], [local], activos));
    }

    [Fact]
    public void Editing_keeps_inactive_assignments_but_rejects_adding_new_inactive_ones()
    {
        var odontologia = Guid.NewGuid();
        var ortodoncia = Guid.NewGuid();
        var nuevaInactiva = Guid.NewGuid();
        var sede = Guid.NewGuid();
        var sedeInactiva = Guid.NewGuid();
        var ficha = Profesional.Crear(
            Guid.NewGuid(),
            "COP 1",
            [odontologia, ortodoncia],
            [sede],
            Activos([odontologia, ortodoncia], [sede]));

        // Ortodoncia y la sede se desactivan después del alta: la ficha queda inactiva y
        // conserva ambas asignaciones, así que administración todavía puede editarla.
        ficha.Desactivar();
        var activos = Activos([odontologia], []);
        ficha.Editar("COP 2", [odontologia, ortodoncia], [sede], activos);
        ficha.Colegiatura.ShouldBe("COP 2");
        ficha.EspecialidadIds.ShouldBe([odontologia, ortodoncia], ignoreOrder: true);
        ficha.LocalIds.ShouldBe([sede]);
        ficha.IsActive.ShouldBeFalse();

        // No puede añadir una especialidad ni un local inactivos, y el rechazo no
        // deja cambios parciales.
        Should.Throw<InvalidOperationException>(() =>
            ficha.Editar("COP 3", [odontologia, ortodoncia, nuevaInactiva], [sede], activos));
        Should.Throw<InvalidOperationException>(() =>
            ficha.Editar("COP 3", [odontologia, ortodoncia], [sede, sedeInactiva], activos));
        ficha.Colegiatura.ShouldBe("COP 2");
        ficha.EspecialidadIds.Count.ShouldBe(2);
        ficha.LocalIds.ShouldBe([sede]);

        // Puede sumar una especialidad activa sin soltar las que ya tenía.
        var nuevaActiva = Guid.NewGuid();
        ficha.Editar("COP 2", [odontologia, ortodoncia, nuevaActiva], [sede], Activos([odontologia, nuevaActiva], []));
        ficha.EspecialidadIds.Count.ShouldBe(3);
        ficha.EspecialidadIds.ShouldContain(nuevaActiva);
    }

    [Fact]
    public void Deactivating_keeps_the_account_and_assignments_and_reactivating_requires_active_ones()
    {
        var accountId = Guid.NewGuid();
        var especialidad = Guid.NewGuid();
        var local = Guid.NewGuid();
        var ficha = Profesional.Crear(
            accountId,
            "COP 1",
            [especialidad],
            [local],
            Activos([especialidad], [local]));

        ficha.Desactivar();

        ficha.IsActive.ShouldBeFalse();
        ficha.StaffAccountId.ShouldBe(accountId);
        ficha.Colegiatura.ShouldBe("COP 1");
        ficha.EspecialidadIds.ShouldBe([especialidad]);
        ficha.LocalIds.ShouldBe([local]);

        // Sin una especialidad activa o sin un local activo la ficha no puede estar activa.
        Should.Throw<InvalidOperationException>(() => ficha.Activar(Activos([], [local])));
        Should.Throw<InvalidOperationException>(() => ficha.Activar(Activos([especialidad], [])));
        ficha.PuedeActivar(Activos([], [local])).ShouldBeFalse();
        ficha.IsActive.ShouldBeFalse();

        var activos = Activos([especialidad], [local]);
        ficha.PuedeActivar(activos).ShouldBeTrue();
        ficha.Activar(activos);

        ficha.IsActive.ShouldBeTrue();
        ficha.StaffAccountId.ShouldBe(accountId);
        ficha.EspecialidadIds.ShouldBe([especialidad]);
        ficha.LocalIds.ShouldBe([local]);
    }

    [Fact]
    public void Editing_an_active_ficha_rejects_leaving_it_without_an_active_specialty_or_local()
    {
        var odontologia = Guid.NewGuid();
        var ortodonciaInactiva = Guid.NewGuid();
        var sede = Guid.NewGuid();
        var sedeInactiva = Guid.NewGuid();
        var ficha = Profesional.Crear(
            Guid.NewGuid(),
            "COP 1",
            [odontologia],
            [sede],
            Activos([odontologia], [sede]));

        // Quitar la última especialidad activa o el último local activo contradice la
        // regla de activación, y el rechazo no deja cambios parciales.
        Should.Throw<InvalidOperationException>(() =>
            ficha.Editar("COP 2", [], [sede], Activos([], [sede])));
        ficha.Colegiatura.ShouldBe("COP 1");
        ficha.EspecialidadIds.ShouldBe([odontologia]);
        ficha.LocalIds.ShouldBe([sede]);

        Should.Throw<InvalidOperationException>(() =>
            ficha.Editar("COP 2", [odontologia], [], Activos([odontologia], [])));
        ficha.Colegiatura.ShouldBe("COP 1");
        ficha.EspecialidadIds.ShouldBe([odontologia]);
        ficha.LocalIds.ShouldBe([sede]);

        // Conservar solo asignaciones inactivas tampoco vale mientras la ficha siga activa.
        Should.Throw<InvalidOperationException>(() =>
            ficha.Editar("COP 2", [ortodonciaInactiva], [sede], Activos([], [sede])));
        Should.Throw<InvalidOperationException>(() =>
            ficha.Editar("COP 2", [odontologia], [sedeInactiva], Activos([odontologia], [])));
        ficha.Colegiatura.ShouldBe("COP 1");
        ficha.EspecialidadIds.ShouldBe([odontologia]);
        ficha.LocalIds.ShouldBe([sede]);

        // Cambiar la última activa por otra activa sigue permitido.
        var nuevaEspecialidad = Guid.NewGuid();
        var nuevoLocal = Guid.NewGuid();
        ficha.Editar(
            "COP 2",
            [nuevaEspecialidad],
            [nuevoLocal],
            Activos([nuevaEspecialidad], [nuevoLocal]));
        ficha.Colegiatura.ShouldBe("COP 2");
        ficha.EspecialidadIds.ShouldBe([nuevaEspecialidad]);
        ficha.LocalIds.ShouldBe([nuevoLocal]);
    }

    [Fact]
    public void Editing_an_inactive_ficha_can_retire_every_assignment_and_the_colegiatura()
    {
        var especialidad = Guid.NewGuid();
        var local = Guid.NewGuid();
        var ficha = Profesional.Crear(
            Guid.NewGuid(),
            "COP 1",
            [especialidad],
            [local],
            Activos([especialidad], [local]));

        ficha.Desactivar();
        ficha.Editar(null, [], [], Activos([], []));

        ficha.IsActive.ShouldBeFalse();
        ficha.Colegiatura.ShouldBeNull();
        ficha.ColegiaturaNormalizada.ShouldBeNull();
        ficha.EspecialidadIds.ShouldBeEmpty();
        ficha.LocalIds.ShouldBeEmpty();
    }

    [Fact]
    public void Editing_retires_the_assignments_that_are_no_longer_asked_for()
    {
        var odontologia = Guid.NewGuid();
        var ortodoncia = Guid.NewGuid();
        var sedeNorte = Guid.NewGuid();
        var sedeSur = Guid.NewGuid();
        var ficha = Profesional.Crear(
            Guid.NewGuid(),
            null,
            [odontologia, ortodoncia],
            [sedeNorte, sedeSur],
            Activos([odontologia, ortodoncia], [sedeNorte, sedeSur]));

        ficha.Editar(null, [odontologia], [sedeSur], Activos([odontologia], [sedeSur]));

        ficha.EspecialidadIds.ShouldBe([odontologia]);
        ficha.LocalIds.ShouldBe([sedeSur]);
    }

    [Fact]
    public void Restoring_derives_the_normalized_colegiatura_from_the_visible_number()
    {
        var ficha = Profesional.Restaurar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  cop 12345 ",
            true,
            [],
            []);

        ficha.Colegiatura.ShouldBe("  cop 12345 ");
        ficha.ColegiaturaNormalizada.ShouldBe("COP12345");

        Profesional.Restaurar(Guid.NewGuid(), Guid.NewGuid(), null, true, [], [])
            .ColegiaturaNormalizada.ShouldBeNull();
        Profesional.Restaurar(Guid.NewGuid(), Guid.NewGuid(), "   ", true, [], [])
            .ColegiaturaNormalizada.ShouldBeNull();
    }

    [Fact]
    public void Deactivating_and_editing_an_inactive_ficha_never_activates_it()
    {
        var especialidad = Guid.NewGuid();
        var local = Guid.NewGuid();
        var ficha = Profesional.Crear(
            Guid.NewGuid(),
            null,
            [especialidad],
            [local],
            Activos([especialidad], [local]));

        ficha.Desactivar();
        ficha.Editar("COP 9", [especialidad], [local], Activos([especialidad], [local]));

        ficha.IsActive.ShouldBeFalse();
        ficha.Colegiatura.ShouldBe("COP 9");
    }

    [Fact]
    public void Creation_and_active_edition_apply_the_same_assignment_rule()
    {
        var activa = Guid.NewGuid();
        var inactiva = Guid.NewGuid();
        var localActivo = Guid.NewGuid();
        var activos = Activos([activa], [localActivo]);

        // Un destino inactivo se rechaza con el mismo motivo en ambas entradas y antes
        // que el mínimo, porque la regla es una sola.
        var creacion = Should.Throw<InvalidOperationException>(() => Profesional.Crear(
            Guid.NewGuid(),
            null,
            [activa, inactiva],
            [localActivo],
            activos));
        var ficha = Profesional.Crear(Guid.NewGuid(), null, [activa], [localActivo], activos);
        var edicion = Should.Throw<InvalidOperationException>(() =>
            ficha.Editar(null, [activa, inactiva], [localActivo], activos));

        edicion.Message.ShouldBe(creacion.Message);
        creacion.Message.ShouldBe("No se puede asignar una especialidad inactiva.");

        // El mínimo de una ficha activa también coincide en ambas entradas.
        var minimoCreacion = Should.Throw<InvalidOperationException>(() => Profesional.Crear(
            Guid.NewGuid(),
            null,
            [activa],
            [],
            Activos([activa], [])));
        var minimoEdicion = Should.Throw<InvalidOperationException>(() =>
            ficha.Editar(null, [activa], [], Activos([activa], [])));

        minimoEdicion.Message.ShouldBe(minimoCreacion.Message);
    }

    private static CatalogoActivo Activos(IEnumerable<Guid> especialidades, IEnumerable<Guid> locales) =>
        new(especialidades, locales);
}
