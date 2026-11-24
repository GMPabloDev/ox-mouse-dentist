using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;
using MouseDent.Infrastructure.Auth;
using MouseDent.Infrastructure.Persistence;
using MouseDent.Infrastructure.Profesionales;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Comprueba la persistencia de la ficha contra la base real: la cuenta y la
/// colegiatura son únicas incluso frente a escrituras simultáneas, las
/// especialidades y los locales son relaciones que sobreviven a su desactivación,
/// y una escritura rechazada no deja datos parciales.
/// </summary>
public sealed class ProfesionalesPersistenceTests
{
    [Fact]
    public async Task The_ficha_is_read_back_with_its_account_colegiatura_and_assignments()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "ficha@mousedent.test");
            var odontologia = await SeedEspecialidadAsync(setup, "Odontología");
            var ortodoncia = await SeedEspecialidadAsync(setup, "Ortodoncia");
            var sede = await SeedLocalAsync(setup, "Sede Ficha");
            var ficha = Profesional.Crear(
                cuenta,
                "  COP 12345  ",
                [ortodoncia, odontologia],
                [sede],
                Activos([odontologia, ortodoncia], [sede]));

            (await new EfProfesionales(setup).GuardarAsync(ficha, token))
                .ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var read = CreateContext(path);
            var store = new EfProfesionales(read);
            var guardada = await store.BuscarAsync(ficha.Id, token);

            guardada.ShouldNotBeNull();
            guardada.StaffAccountId.ShouldBe(cuenta);
            guardada.IsActive.ShouldBeTrue();
            guardada.Colegiatura.ShouldBe("COP 12345");
            guardada.ColegiaturaNormalizada.ShouldBe("COP12345");
            guardada.EspecialidadIds.ShouldBe([odontologia, ortodoncia], ignoreOrder: true);
            guardada.LocalIds.ShouldBe([sede]);

            var porCuenta = await store.BuscarPorCuentaAsync(cuenta, token);

            porCuenta.ShouldNotBeNull();
            porCuenta.Id.ShouldBe(ficha.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task The_colegiatura_is_unique_between_all_fichas_including_the_inactive_ones()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Colegiatura");
            var store = new EfProfesionales(setup);
            var primera = Profesional.Crear(
                await SeedProfessionalAccountAsync(setup, "primera@mousedent.test"),
                "COP 12345",
                [especialidad],
                [local],
                Activos([especialidad], [local]));

            (await store.GuardarAsync(primera, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            // La ficha inactiva sigue ocupando el número.
            primera.Desactivar();
            (await store.GuardarAsync(primera, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var copia = Profesional.Crear(
                await SeedProfessionalAccountAsync(setup, "copia@mousedent.test"),
                "  cop12345 ",
                [especialidad],
                [local],
                Activos([especialidad], [local]));

            (await store.GuardarAsync(copia, token))
                .ShouldBe(ResultadoGuardadoProfesional.ColegiaturaDuplicada);

            await using var verify = CreateContext(path);

            (await verify.Profesionales.CountAsync(token)).ShouldBe(1);
            (await verify.Profesionales.SingleAsync(token)).ColegiaturaNormalizada.ShouldBe("COP12345");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Several_fichas_can_have_no_colegiatura()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Sin Número");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(
                    await SeedProfessionalAccountAsync(setup, "sin-numero-uno@mousedent.test"),
                    null,
                    [especialidad],
                    [local],
                    Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            (await store.GuardarAsync(
                Profesional.Crear(
                    await SeedProfessionalAccountAsync(setup, "sin-numero-dos@mousedent.test"),
                    "   ",
                    [especialidad],
                    [local],
                    Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var verify = CreateContext(path);

            (await verify.Profesionales.CountAsync(token)).ShouldBe(2);
            (await verify.Profesionales.CountAsync(ficha => ficha.ColegiaturaNormalizada == null, token))
                .ShouldBe(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task The_account_can_have_only_one_ficha()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Única");
            var cuenta = await SeedProfessionalAccountAsync(setup, "unica@mousedent.test");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [local], Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var segunda = Profesional.Crear(
                cuenta,
                "COP 2",
                [especialidad],
                [local],
                Activos([especialidad], [local]));

            (await store.GuardarAsync(segunda, token))
                .ShouldBe(ResultadoGuardadoProfesional.CuentaYaVinculada);

            await using var verify = CreateContext(path);

            (await verify.Profesionales.CountAsync(ficha => ficha.StaffAccountId == cuenta, token)).ShouldBe(1);

            // La base también lo impide cuando la escritura no pasa por el almacén.
            verify.Profesionales.Add(new ProfesionalGuardado
            {
                Id = Guid.NewGuid(),
                StaffAccountId = cuenta,
                IsActive = true,
            });

            await Should.ThrowAsync<DbUpdateException>(
                async () => await verify.SaveChangesAsync(token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_rejected_save_leaves_no_partial_changes()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var otroLocal = await SeedLocalAsync(setup, "Sede Rechazo");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(
                    await SeedProfessionalAccountAsync(setup, "aceptada@mousedent.test"),
                    "COP 1",
                    [especialidad],
                    [otroLocal],
                    Activos([especialidad], [otroLocal])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var rechazada = Profesional.Crear(
                await SeedProfessionalAccountAsync(setup, "rechazada@mousedent.test"),
                "cop 1",
                [especialidad],
                [otroLocal],
                Activos([especialidad], [otroLocal]));

            (await store.GuardarAsync(rechazada, token))
                .ShouldBe(ResultadoGuardadoProfesional.ColegiaturaDuplicada);

            // Ni la ficha ni sus asignaciones quedan pendientes para un guardado posterior.
            await setup.SaveChangesAsync(token);
            await using var verify = CreateContext(path);

            (await verify.Profesionales.CountAsync(token)).ShouldBe(1);
            (await verify.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(1);
            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Assignments_must_point_to_existing_specialties_and_locales()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var local = await SeedLocalAsync(setup, "Sede Relación");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var cuenta = await SeedProfessionalAccountAsync(setup, "relacion@mousedent.test");
            var store = new EfProfesionales(setup);
            var desconocida = Guid.NewGuid();

            var sinEspecialidad = Profesional.Crear(
                cuenta,
                null,
                [desconocida],
                [local],
                Activos([desconocida], [local]));

            await Should.ThrowAsync<DbUpdateException>(
                async () => await store.GuardarAsync(sinEspecialidad, token));

            var desconocido = Guid.NewGuid();
            var sinLocal = Profesional.Crear(
                cuenta,
                null,
                [especialidad],
                [desconocido],
                Activos([especialidad], [desconocido]));

            await Should.ThrowAsync<DbUpdateException>(
                async () => await store.GuardarAsync(sinLocal, token));

            // Las dos fichas rechazadas dejan la base como estaba y el contexto limpio.
            await setup.SaveChangesAsync(token);
            await using var verify = CreateContext(path);

            (await verify.Profesionales.CountAsync(token)).ShouldBe(0);
            (await verify.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(0);
            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Deactivating_keeps_the_assignments_and_reactivating_requires_active_ones()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "reactivacion@mousedent.test");
            var odontologia = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Reactivación");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 7", [odontologia], [local], Activos([odontologia], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var ficha = await store.BuscarPorCuentaAsync(cuenta, token);
            ficha.ShouldNotBeNull();

            ficha.Desactivar();
            (await store.GuardarAsync(ficha, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using (var inactive = CreateContext(path))
            {
                var guardada = await new EfProfesionales(inactive).BuscarPorCuentaAsync(cuenta, token);

                guardada.ShouldNotBeNull();
                guardada.IsActive.ShouldBeFalse();
                guardada.StaffAccountId.ShouldBe(cuenta);
                guardada.Colegiatura.ShouldBe("COP 7");
                guardada.EspecialidadIds.ShouldBe([odontologia]);
                guardada.LocalIds.ShouldBe([local]);
            }

            // La especialidad se desactiva en el catálogo: la asignación se conserva,
            // pero la ficha no puede reactivarse sin una especialidad activa.
            var ortodoncia = await SeedEspecialidadAsync(setup, "Ortodoncia");
            var odontologiaGuardada = await setup.Especialidades.SingleAsync(
                especialidad => especialidad.Id == odontologia,
                token);
            odontologiaGuardada.Deactivate();
            await setup.SaveChangesAsync(token);

            var sinActivos = await new EfProfesionales(setup).BuscarPorCuentaAsync(cuenta, token);
            sinActivos.ShouldNotBeNull();
            sinActivos.EspecialidadIds.ShouldBe([odontologia]);
            sinActivos.IsActive.ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => sinActivos.Activar(Activos([], [local])));

            sinActivos.Editar("COP 7", [odontologia, ortodoncia], [local], Activos([ortodoncia], [local]));
            sinActivos.Activar(Activos([ortodoncia], [local]));
            (await store.GuardarAsync(sinActivos, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var verify = CreateContext(path);
            var reactivada = await new EfProfesionales(verify).BuscarPorCuentaAsync(cuenta, token);

            reactivada.ShouldNotBeNull();
            reactivada.IsActive.ShouldBeTrue();
            reactivada.EspecialidadIds.ShouldBe([odontologia, ortodoncia], ignoreOrder: true);
            reactivada.LocalIds.ShouldBe([local]);
            (await verify.Especialidades.SingleAsync(especialidad => especialidad.Id == odontologia, token))
                .IsActive.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Concurrent_saves_cannot_duplicate_the_colegiatura()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Concurrente");
            var primeraCuenta = await SeedProfessionalAccountAsync(setup, "concurrente-uno@mousedent.test");
            var segundaCuenta = await SeedProfessionalAccountAsync(setup, "concurrente-dos@mousedent.test");
            var activos = Activos([especialidad], [local]);

            using var gate = new ManualResetEventSlim();

            async Task<ResultadoGuardadoProfesional> Crear(string colegiatura, Guid cuenta)
            {
                await Task.Yield();
                gate.Wait(token);
                await using var database = CreateContext(path);

                return await new EfProfesionales(database).GuardarAsync(
                    Profesional.Crear(cuenta, colegiatura, [especialidad], [local], activos),
                    token);
            }

            var primera = Task.Run(() => Crear("COP 12345", primeraCuenta), token);
            var segunda = Task.Run(() => Crear(" cop12345 ", segundaCuenta), token);
            gate.Set();

            var results = await Task.WhenAll(primera, segunda);

            results.Count(result => result == ResultadoGuardadoProfesional.Guardado).ShouldBe(1);
            results.Count(result => result == ResultadoGuardadoProfesional.ColegiaturaDuplicada).ShouldBe(1);
            (await setup.Profesionales.CountAsync(token)).ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Concurrent_saves_cannot_create_two_fichas_for_the_same_account()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Misma Cuenta");
            var cuenta = await SeedProfessionalAccountAsync(setup, "misma-cuenta@mousedent.test");
            var activos = Activos([especialidad], [local]);

            using var gate = new ManualResetEventSlim();

            async Task<ResultadoGuardadoProfesional> Crear(string colegiatura)
            {
                await Task.Yield();
                gate.Wait(token);
                await using var database = CreateContext(path);

                return await new EfProfesionales(database).GuardarAsync(
                    Profesional.Crear(cuenta, colegiatura, [especialidad], [local], activos),
                    token);
            }

            var primera = Task.Run(() => Crear("COP 1"), token);
            var segunda = Task.Run(() => Crear("COP 2"), token);
            gate.Set();

            var results = await Task.WhenAll(primera, segunda);

            results.Count(result => result == ResultadoGuardadoProfesional.Guardado).ShouldBe(1);
            results.Count(result => result == ResultadoGuardadoProfesional.CuentaYaVinculada).ShouldBe(1);
            (await setup.Profesionales.CountAsync(ficha => ficha.StaffAccountId == cuenta, token)).ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_stale_writer_cannot_assign_a_specialty_deactivated_meanwhile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "guardia-especialidad@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Guardia Especialidad");

            // Quien crea la ficha leyó el catálogo antes de la desactivación y cree que la
            // especialidad sigue activa.
            var leidos = Activos([especialidad], [local]);
            await DeactivateEspecialidadAsync(path, especialidad);

            var ficha = Profesional.Crear(cuenta, "COP 1", [especialidad], [local], leidos);

            (await new EfProfesionales(setup).GuardarAsync(ficha, token))
                .ShouldBe(ResultadoGuardadoProfesional.EspecialidadInactiva);

            // El rechazo deja la base como estaba y no queda nada pendiente en el contexto.
            await setup.SaveChangesAsync(token);

            (await setup.Profesionales.CountAsync(token)).ShouldBe(0);
            (await setup.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(0);
            (await setup.ProfesionalLocales.CountAsync(token)).ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_stale_writer_cannot_add_a_local_deactivated_meanwhile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "guardia-local@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var sede = await SeedLocalAsync(setup, "Sede Guardia Local");
            var otraSede = await SeedLocalAsync(setup, "Sede Guardia Nueva");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [sede], Activos([especialidad], [sede])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            // La segunda sede se desactiva después de que el formulario la ofreciera.
            var leidos = Activos([especialidad], [sede, otraSede]);
            await DeactivateLocalAsync(path, otraSede);

            var ficha = await store.BuscarPorCuentaAsync(cuenta, token);
            ficha.ShouldNotBeNull();
            ficha.Editar(null, [especialidad], [sede, otraSede], leidos);

            (await store.GuardarAsync(ficha, token)).ShouldBe(ResultadoGuardadoProfesional.LocalInactivo);

            await setup.SaveChangesAsync(token);
            await using var verify = CreateContext(path);

            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(1);
            (await verify.ProfesionalLocales.CountAsync(fila => fila.LocalId == otraSede, token)).ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_stale_reactivation_is_rejected_when_the_specialty_went_inactive_meanwhile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "reactivacion-stale@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Reactivación Stale");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [local], Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var guardada = await store.BuscarPorCuentaAsync(cuenta, token);
            guardada.ShouldNotBeNull();
            guardada.Desactivar();
            (await store.GuardarAsync(guardada, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            // Administración conserva la copia del catálogo anterior a la desactivación:
            // el dominio la acepta y la guarda tiene que rechazarla.
            var leidos = Activos([especialidad], [local]);
            await DeactivateEspecialidadAsync(path, especialidad);

            var ficha = await store.BuscarPorCuentaAsync(cuenta, token);
            ficha.ShouldNotBeNull();
            ficha.Activar(leidos);

            (await store.GuardarAsync(ficha, token))
                .ShouldBe(ResultadoGuardadoProfesional.ActivacionIncompleta);

            await using var verify = CreateContext(path);
            var conservada = await new EfProfesionales(verify).BuscarPorCuentaAsync(cuenta, token);

            conservada.ShouldNotBeNull();
            conservada.IsActive.ShouldBeFalse();
            conservada.EspecialidadIds.ShouldBe([especialidad]);
            conservada.LocalIds.ShouldBe([local]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Deactivating_an_assigned_local_keeps_it_and_blocks_reactivation()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "local-inactivo@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var sede = await SeedLocalAsync(setup, "Sede Inactivada");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [sede], Activos([especialidad], [sede])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var local = await setup.Locales.SingleAsync(asignado => asignado.Id == sede, token);
            local.Deactivate();
            await setup.SaveChangesAsync(token);

            var ficha = await store.BuscarPorCuentaAsync(cuenta, token);
            ficha.ShouldNotBeNull();
            ficha.LocalIds.ShouldBe([sede]);

            ficha.Desactivar();
            (await store.GuardarAsync(ficha, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var inactiva = await store.BuscarPorCuentaAsync(cuenta, token);
            inactiva.ShouldNotBeNull();

            // La copia del catálogo ya no sirve para activar.
            Should.Throw<InvalidOperationException>(() => inactiva.Activar(Activos([especialidad], [])));

            // Aunque el llamador crea que el local sigue activo, la base rechaza la activación.
            inactiva.Activar(Activos([especialidad], [sede]));
            (await store.GuardarAsync(inactiva, token))
                .ShouldBe(ResultadoGuardadoProfesional.ActivacionIncompleta);

            // Con un local activo la ficha vuelve a activarse y conserva la asignación inactiva.
            var otraSede = await SeedLocalAsync(setup, "Sede Activa");
            var recuperada = await store.BuscarPorCuentaAsync(cuenta, token);
            recuperada.ShouldNotBeNull();
            recuperada.Editar(null, [especialidad], [sede, otraSede], Activos([especialidad], [otraSede]));
            recuperada.Activar(Activos([especialidad], [otraSede]));
            (await store.GuardarAsync(recuperada, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var verify = CreateContext(path);
            var reactivada = await new EfProfesionales(verify).BuscarPorCuentaAsync(cuenta, token);

            reactivada.ShouldNotBeNull();
            reactivada.IsActive.ShouldBeTrue();
            reactivada.LocalIds.ShouldBe([sede, otraSede], ignoreOrder: true);
            (await verify.Locales.SingleAsync(asignado => asignado.Id == sede, token)).IsActive.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Keeping_inactive_assignments_does_not_trip_the_write_guard()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "retencion@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var sede = await SeedLocalAsync(setup, "Sede Retención");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [sede], Activos([especialidad], [sede])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await DeactivateEspecialidadAsync(path, especialidad);
            await DeactivateLocalAsync(path, sede);

            // La ficha inactiva conserva asignaciones inactivas y se puede seguir editando.
            var ficha = await store.BuscarPorCuentaAsync(cuenta, token);
            ficha.ShouldNotBeNull();
            ficha.Desactivar();
            (await store.GuardarAsync(ficha, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            ficha.Editar("COP 2", [especialidad], [sede], Activos([], []));
            (await store.GuardarAsync(ficha, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var verify = CreateContext(path);
            var conservada = await new EfProfesionales(verify).BuscarPorCuentaAsync(cuenta, token);

            conservada.ShouldNotBeNull();
            conservada.Colegiatura.ShouldBe("COP 2");
            conservada.EspecialidadIds.ShouldBe([especialidad]);
            conservada.LocalIds.ShouldBe([sede]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Editing_retires_the_assignments_and_leaves_no_rows_behind()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "retiro@mousedent.test");
            var odontologia = await SeedEspecialidadAsync(setup, "Odontología");
            var ortodoncia = await SeedEspecialidadAsync(setup, "Ortodoncia");
            var sedeNorte = await SeedLocalAsync(setup, "Sede Norte");
            var sedeSur = await SeedLocalAsync(setup, "Sede Sur");
            var store = new EfProfesionales(setup);
            var especialidades = new[] { odontologia, ortodoncia };
            var locales = new[] { sedeNorte, sedeSur };

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", especialidades, locales, Activos(especialidades, locales)),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var ficha = await store.BuscarPorCuentaAsync(cuenta, token);
            ficha.ShouldNotBeNull();
            ficha.Editar("COP 1", [odontologia], [sedeSur], Activos([odontologia], [sedeSur]));
            (await store.GuardarAsync(ficha, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var verify = CreateContext(path);
            var guardada = await new EfProfesionales(verify).BuscarAsync(ficha.Id, token);

            guardada.ShouldNotBeNull();
            guardada.EspecialidadIds.ShouldBe([odontologia]);
            guardada.LocalIds.ShouldBe([sedeSur]);
            (await verify.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(1);
            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(1);
            (await verify.ProfesionalEspecialidades.SingleAsync(token)).EspecialidadId.ShouldBe(odontologia);
            (await verify.ProfesionalLocales.SingleAsync(token)).LocalId.ShouldBe(sedeSur);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task The_ficha_requires_an_existing_account()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Cuenta");
            var ficha = Profesional.Crear(
                Guid.NewGuid(),
                "COP 1",
                [especialidad],
                [local],
                Activos([especialidad], [local]));

            await Should.ThrowAsync<DbUpdateException>(
                async () => await new EfProfesionales(setup).GuardarAsync(ficha, token));

            // Un guardado de control no vuelve a intentar la ficha rechazada.
            await setup.SaveChangesAsync(token);
            await using var verify = CreateContext(path);

            (await verify.Profesionales.CountAsync(token)).ShouldBe(0);
            (await verify.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(0);
            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Reading_the_ficha_with_both_collections_is_a_split_query_without_the_20504_warning()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "lectura-separada@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Lectura Separada");
            var ficha = Profesional.Crear(
                cuenta,
                "COP 1",
                [especialidad],
                [local],
                Activos([especialidad], [local]));

            (await new EfProfesionales(setup).GuardarAsync(ficha, token))
                .ShouldBe(ResultadoGuardadoProfesional.Guardado);

            // El aviso 20504 se convierte en error: si la lectura o el guardado vuelven a
            // usar una sola consulta con las dos colecciones, la prueba falla.
            var options = new DbContextOptionsBuilder<MouseDentDbContext>()
                .UseSqlite($"Data Source={path}")
                .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
                .Options;

            await using var read = new MouseDentDbContext(options);
            var store = new EfProfesionales(read);
            var leida = await store.BuscarAsync(ficha.Id, token);

            leida.ShouldNotBeNull();
            leida.EspecialidadIds.ShouldBe([especialidad]);
            leida.LocalIds.ShouldBe([local]);

            leida.Editar("COP 2", [especialidad], [local], Activos([especialidad], [local]));
            (await store.GuardarAsync(leida, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_stale_edit_cannot_leave_an_active_ficha_without_active_assignments()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "borrado-intercalado@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Borrado Intercalado");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [local], Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var desactivada = await store.BuscarPorCuentaAsync(cuenta, token);
            desactivada.ShouldNotBeNull();
            desactivada.Desactivar();
            (await store.GuardarAsync(desactivada, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            // La solicitud A abre la edición de la ficha inactiva y su escritura ya tiene
            // rastreada esa versión cuando otra solicitud reactiva la ficha.
            await using var contextoA = CreateContext(path);
            var copiaA = await new EfProfesionales(contextoA).BuscarPorCuentaAsync(cuenta, token);
            copiaA.ShouldNotBeNull();
            copiaA.IsActive.ShouldBeFalse();
            await contextoA.Profesionales
                .Include(ficha => ficha.Especialidades)
                .Include(ficha => ficha.Locales)
                .SingleAsync(ficha => ficha.StaffAccountId == cuenta, token);

            await using (var contextoB = CreateContext(path))
            {
                var storeB = new EfProfesionales(contextoB);
                var copiaB = await storeB.BuscarPorCuentaAsync(cuenta, token);
                copiaB.ShouldNotBeNull();
                copiaB.Activar(Activos([especialidad], [local]));
                (await storeB.GuardarAsync(copiaB, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);
            }

            // A escribe desde su copia inactiva: retira las asignaciones sin cambiar IsActive,
            // así que la reactivación de B quedaría sin especialidad ni local activos.
            copiaA.Editar(null, [], [], Activos([], []));
            (await new EfProfesionales(contextoA).GuardarAsync(copiaA, token))
                .ShouldBe(ResultadoGuardadoProfesional.ActivacionIncompleta);

            await using var verify = CreateContext(path);
            var conservada = await new EfProfesionales(verify).BuscarPorCuentaAsync(cuenta, token);

            conservada.ShouldNotBeNull();
            conservada.IsActive.ShouldBeTrue();
            conservada.EspecialidadIds.ShouldBe([especialidad]);
            conservada.LocalIds.ShouldBe([local]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_stale_inactive_edit_that_deactivates_the_ficha_can_retire_its_assignments()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "borrado-desactivacion@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Borrado Desactivación");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [local], Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            var desactivada = await store.BuscarPorCuentaAsync(cuenta, token);
            desactivada.ShouldNotBeNull();
            desactivada.Desactivar();
            (await store.GuardarAsync(desactivada, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var contextoA = CreateContext(path);
            var copiaA = await new EfProfesionales(contextoA).BuscarPorCuentaAsync(cuenta, token);
            copiaA.ShouldNotBeNull();
            copiaA.IsActive.ShouldBeFalse();

            await using (var contextoB = CreateContext(path))
            {
                var storeB = new EfProfesionales(contextoB);
                var copiaB = await storeB.BuscarPorCuentaAsync(cuenta, token);
                copiaB.ShouldNotBeNull();
                copiaB.Activar(Activos([especialidad], [local]));
                (await storeB.GuardarAsync(copiaB, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);
            }

            // A escribe con una copia inactiva y la escritura carga la ficha ya activa: la
            // deja inactiva antes de borrar, así que el resultado es válido y no debe abortar.
            copiaA.Editar(null, [], [], Activos([], []));
            (await new EfProfesionales(contextoA).GuardarAsync(copiaA, token))
                .ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var verify = CreateContext(path);
            var guardada = await new EfProfesionales(verify).BuscarPorCuentaAsync(cuenta, token);

            guardada.ShouldNotBeNull();
            guardada.IsActive.ShouldBeFalse();
            guardada.EspecialidadIds.ShouldBeEmpty();
            guardada.LocalIds.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Deleting_the_last_active_specialty_of_an_active_ficha_is_rejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "borrado-ultima-especialidad@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Última Especialidad");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [local], Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using (var delete = CreateContext(path))
            {
                delete.ProfesionalEspecialidades.Remove(await delete.ProfesionalEspecialidades
                    .SingleAsync(asignada => asignada.EspecialidadId == especialidad, token));

                var error = await Should.ThrowAsync<DbUpdateException>(
                    async () => await delete.SaveChangesAsync(token));

                error.InnerException.ShouldNotBeNull();
                error.InnerException.Message.ShouldContain("profesional_activacion_incompleta");
            }

            await using var verify = CreateContext(path);

            (await verify.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(1);
            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Deleting_the_last_active_local_of_an_active_ficha_is_rejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "borrado-ultimo-local@mousedent.test");
            var especialidad = await SeedEspecialidadAsync(setup, "Odontología");
            var local = await SeedLocalAsync(setup, "Sede Último Local");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [especialidad], [local], Activos([especialidad], [local])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using (var delete = CreateContext(path))
            {
                delete.ProfesionalLocales.Remove(await delete.ProfesionalLocales
                    .SingleAsync(asignado => asignado.LocalId == local, token));

                var error = await Should.ThrowAsync<DbUpdateException>(
                    async () => await delete.SaveChangesAsync(token));

                error.InnerException.ShouldNotBeNull();
                error.InnerException.Message.ShouldContain("profesional_activacion_incompleta");
            }

            await using var verify = CreateContext(path);

            (await verify.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(1);
            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Deleting_assignments_that_leave_another_active_one_is_allowed()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "borrado-conserva@mousedent.test");
            var odontologia = await SeedEspecialidadAsync(setup, "Odontología");
            var ortodoncia = await SeedEspecialidadAsync(setup, "Ortodoncia");
            var norte = await SeedLocalAsync(setup, "Sede Norte Borrado");
            var sur = await SeedLocalAsync(setup, "Sede Sur Borrado");
            var store = new EfProfesionales(setup);
            var especialidades = new[] { odontologia, ortodoncia };
            var locales = new[] { norte, sur };

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", especialidades, locales, Activos(especialidades, locales)),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using (var delete = CreateContext(path))
            {
                delete.ProfesionalEspecialidades.Remove(await delete.ProfesionalEspecialidades
                    .SingleAsync(asignada => asignada.EspecialidadId == odontologia, token));
                delete.ProfesionalLocales.Remove(await delete.ProfesionalLocales
                    .SingleAsync(asignado => asignado.LocalId == norte, token));

                await delete.SaveChangesAsync(token);
            }

            await using var verify = CreateContext(path);

            (await verify.ProfesionalEspecialidades.SingleAsync(token)).EspecialidadId.ShouldBe(ortodoncia);
            (await verify.ProfesionalLocales.SingleAsync(token)).LocalId.ShouldBe(sur);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Deleting_inactive_assignments_does_not_trip_the_delete_guard()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "borrado-retencion@mousedent.test");
            var activa = await SeedEspecialidadAsync(setup, "Odontología");
            var inactiva = await SeedEspecialidadAsync(setup, "Ortodoncia");
            var activo = await SeedLocalAsync(setup, "Sede Activa Borrado");
            var inactivo = await SeedLocalAsync(setup, "Sede Inactiva Borrado");
            var store = new EfProfesionales(setup);
            var especialidades = new[] { activa, inactiva };
            var locales = new[] { activo, inactivo };

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", especialidades, locales, Activos(especialidades, locales)),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            // Las asignaciones inactivas se retiran sin tocar las activas de la ficha.
            await DeactivateEspecialidadAsync(path, inactiva);
            await DeactivateLocalAsync(path, inactivo);

            await using (var delete = CreateContext(path))
            {
                delete.ProfesionalEspecialidades.Remove(await delete.ProfesionalEspecialidades
                    .SingleAsync(asignada => asignada.EspecialidadId == inactiva, token));
                delete.ProfesionalLocales.Remove(await delete.ProfesionalLocales
                    .SingleAsync(asignado => asignado.LocalId == inactivo, token));

                await delete.SaveChangesAsync(token);
            }

            // Una ficha inactiva puede quedarse sin asignaciones activas.
            setup.ChangeTracker.Clear();
            var inactivaFicha = await store.BuscarPorCuentaAsync(cuenta, token);
            inactivaFicha.ShouldNotBeNull();
            inactivaFicha.Desactivar();
            (await store.GuardarAsync(inactivaFicha, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);
            await DeactivateEspecialidadAsync(path, activa);
            await DeactivateLocalAsync(path, activo);

            await using (var delete = CreateContext(path))
            {
                delete.ProfesionalEspecialidades.RemoveRange(delete.ProfesionalEspecialidades);
                delete.ProfesionalLocales.RemoveRange(delete.ProfesionalLocales);

                await delete.SaveChangesAsync(token);
            }

            await using var verify = CreateContext(path);

            (await verify.ProfesionalEspecialidades.CountAsync(token)).ShouldBe(0);
            (await verify.ProfesionalLocales.CountAsync(token)).ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Replacing_the_last_active_assignments_in_one_save_is_allowed()
    {
        var path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using var setup = CreateContext(path);
            await setup.Database.MigrateAsync(token);

            var cuenta = await SeedProfessionalAccountAsync(setup, "borrado-intercambio@mousedent.test");
            var odontologia = await SeedEspecialidadAsync(setup, "Odontología");
            var ortodoncia = await SeedEspecialidadAsync(setup, "Ortodoncia");
            var norte = await SeedLocalAsync(setup, "Sede Norte Intercambio");
            var sur = await SeedLocalAsync(setup, "Sede Sur Intercambio");
            var store = new EfProfesionales(setup);

            (await store.GuardarAsync(
                Profesional.Crear(cuenta, "COP 1", [odontologia], [norte], Activos([odontologia], [norte])),
                token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            // Cambiar la única especialidad y el único local activos por otros activos es
            // una edición válida y no puede confundirse con un borrado que deja la ficha vacía.
            var ficha = await store.BuscarPorCuentaAsync(cuenta, token);
            ficha.ShouldNotBeNull();
            ficha.Editar(null, [ortodoncia], [sur], Activos([ortodoncia], [sur]));

            (await store.GuardarAsync(ficha, token)).ShouldBe(ResultadoGuardadoProfesional.Guardado);

            await using var verify = CreateContext(path);
            var guardada = await new EfProfesionales(verify).BuscarPorCuentaAsync(cuenta, token);

            guardada.ShouldNotBeNull();
            guardada.EspecialidadIds.ShouldBe([ortodoncia]);
            guardada.LocalIds.ShouldBe([sur]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Migration_creates_and_removes_the_write_guard_triggers()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var database = CreateContext(path);
            var token = TestContext.Current.CancellationToken;
            await database.Database.MigrateAsync(token);

            (await ReadProfesionalTriggersAsync(database, token)).ShouldBe(
                [
                    "Profesional_Activar",
                    "Profesional_Especialidad_Asignar",
                    "Profesional_Especialidad_Borrar",
                    "Profesional_Local_Asignar",
                    "Profesional_Local_Borrar",
                ],
                ignoreOrder: true);

            // El Down de la migración de borrado retira sus dos triggers y conserva los tres
            // que crea ProfesionalReglasDeEscritura.
            await database.GetService<IMigrator>().MigrateAsync("20261123212530_CorreoUnico", token);

            (await ReadProfesionalTriggersAsync(database, token)).ShouldBe(
                ["Profesional_Activar", "Profesional_Especialidad_Asignar", "Profesional_Local_Asignar"],
                ignoreOrder: true);

            await database.GetService<IMigrator>().MigrateAsync("20261123194253_Profesional", token);

            (await ReadProfesionalTriggersAsync(database, token)).ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Task<List<string>> ReadProfesionalTriggersAsync(
        MouseDentDbContext database,
        CancellationToken cancellationToken) =>
        database.Database
            .SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'Profesional%'")
            .ToListAsync(cancellationToken);

    private static async Task DeactivateEspecialidadAsync(string path, Guid especialidadId)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateContext(path);
        var especialidad = await database.Especialidades.SingleAsync(
            guardada => guardada.Id == especialidadId,
            token);
        especialidad.Deactivate();
        await database.SaveChangesAsync(token);
    }

    private static async Task DeactivateLocalAsync(string path, Guid localId)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateContext(path);
        var local = await database.Locales.SingleAsync(guardado => guardado.Id == localId, token);
        local.Deactivate();
        await database.SaveChangesAsync(token);
    }

    private static MouseDentDbContext CreateContext(string path) => new(
        new DbContextOptionsBuilder<MouseDentDbContext>().UseSqlite($"Data Source={path}").Options);

    private static CatalogoActivo Activos(IEnumerable<Guid> especialidades, IEnumerable<Guid> locales) =>
        new(especialidades, locales);

    private static async Task<Guid> SeedProfessionalAccountAsync(MouseDentDbContext database, string email)
    {
        var cuenta = new StaffUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            DisplayName = "Profesional de pruebas",
            Profile = StaffRole.Professional,
            IsActive = true,
            PasswordEstablishedAt = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc),
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        database.Users.Add(cuenta);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        return cuenta.Id;
    }

    private static async Task<Guid> SeedEspecialidadAsync(MouseDentDbContext database, string name)
    {
        var especialidad = Especialidad.Create(name, null);
        database.Especialidades.Add(especialidad);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        return especialidad.Id;
    }

    private static async Task<Guid> SeedLocalAsync(MouseDentDbContext database, string name)
    {
        var schedule = WeeklySchedule.Parse(
            WeeklySchedule.WeekDays.Select(day =>
                new ScheduleDayInput(day, false, new TimeOnly(9, 0), new TimeOnly(18, 0)))).Schedule!;
        var local = Local.Create(name, "Av. Siempre Viva 742", "+51 999 888 777", schedule);
        database.Locales.Add(local);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        return local.Id;
    }
}
