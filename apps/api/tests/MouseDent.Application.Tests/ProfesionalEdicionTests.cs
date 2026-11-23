using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;
using MouseDent.Domain.Locales;
using MouseDent.Domain.Profesionales;
using Shouldly;

namespace MouseDent.Application.Tests;

/// <summary>
/// Decisiones de la edición y del cambio de estado de la ficha. Las pruebas
/// comprueban qué se escribe, qué se conserva y qué se rechaza antes de escribir:
/// la ficha activa mantiene el mínimo activo y la inactiva conserva sus
/// asignaciones inactivas hasta que se pueda reactivar.
/// </summary>
public sealed class ProfesionalEdicionTests
{
    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_edit_saves_the_colegiatura_and_the_assignments_it_received()
    {
        var contexto = Contexto.Crear();

        var outcome = await contexto.Service.UpdateAsync(
            contexto.Ficha.Id,
            new ProfesionalEdicionCommand("  COP 99  ", [contexto.OtraEspecialidad.Id], [contexto.OtroLocal.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalEdicionStatus.Saved);
        contexto.Ficha.Colegiatura.ShouldBe("COP 99");
        contexto.Ficha.ColegiaturaNormalizada.ShouldBe("COP99");
        contexto.Ficha.EspecialidadIds.ShouldBe([contexto.OtraEspecialidad.Id]);
        contexto.Ficha.LocalIds.ShouldBe([contexto.OtroLocal.Id]);
        contexto.Ficha.IsActive.ShouldBeTrue();
        contexto.Fichas.Saved.ShouldHaveSingleItem().ShouldBeSameAs(contexto.Ficha);
        contexto.Accounts.Updated.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_edit_of_an_unknown_ficha_is_rejected()
    {
        var contexto = Contexto.Crear();

        var outcome = await contexto.Service.UpdateAsync(
            Guid.NewGuid(),
            new ProfesionalEdicionCommand(null, [contexto.Especialidad.Id], [contexto.Local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalEdicionStatus.NotFound);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_colegiatura_over_the_limit_is_rejected_without_writing()
    {
        var contexto = Contexto.Crear();

        var outcome = await contexto.Service.UpdateAsync(
            contexto.Ficha.Id,
            new ProfesionalEdicionCommand(
                new string('C', DatosDeColegiatura.LongitudMaxima + 1),
                [contexto.Especialidad.Id],
                [contexto.Local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalEdicionStatus.InvalidColegiatura);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("inactiva")]
    [InlineData("inexistente")]
    public async Task A_new_inactive_or_unknown_specialty_is_rejected_without_writing(string caso)
    {
        var contexto = Contexto.Crear();
        var nueva = caso == "inactiva" ? contexto.OtraEspecialidad.Id : Guid.NewGuid();
        if (caso == "inactiva")
        {
            contexto.OtraEspecialidad.Deactivate();
        }

        var outcome = await contexto.Service.UpdateAsync(
            contexto.Ficha.Id,
            new ProfesionalEdicionCommand(
                null,
                [contexto.Especialidad.Id, nueva],
                [contexto.Local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalEdicionStatus.EspecialidadInactiva);
        contexto.Ficha.EspecialidadIds.ShouldBe([contexto.Especialidad.Id]);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("inactivo")]
    [InlineData("inexistente")]
    public async Task A_new_inactive_or_unknown_local_is_rejected_without_writing(string caso)
    {
        var contexto = Contexto.Crear();
        var nuevo = caso == "inactivo" ? contexto.OtroLocal.Id : Guid.NewGuid();
        if (caso == "inactivo")
        {
            contexto.OtroLocal.Deactivate();
        }

        var outcome = await contexto.Service.UpdateAsync(
            contexto.Ficha.Id,
            new ProfesionalEdicionCommand(
                null,
                [contexto.Especialidad.Id],
                [contexto.Local.Id, nuevo]),
            Token);

        outcome.Status.ShouldBe(ProfesionalEdicionStatus.LocalInactivo);
        contexto.Ficha.LocalIds.ShouldBe([contexto.Local.Id]);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_active_ficha_cannot_lose_its_last_active_specialty_or_local()
    {
        var contexto = Contexto.Crear();
        contexto.Especialidad.Deactivate();
        contexto.Local.Deactivate();

        var outcome = await contexto.Service.UpdateAsync(
            contexto.Ficha.Id,
            new ProfesionalEdicionCommand(
                null,
                [contexto.Especialidad.Id],
                [contexto.Local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalEdicionStatus.ActivacionIncompleta);
        contexto.Ficha.IsActive.ShouldBeTrue();
        contexto.Ficha.EspecialidadIds.ShouldBe([contexto.Especialidad.Id]);
        contexto.Ficha.LocalIds.ShouldBe([contexto.Local.Id]);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_inactive_ficha_is_edited_with_its_inactive_assignments()
    {
        var contexto = Contexto.Crear();
        contexto.Ficha.Desactivar();
        contexto.Especialidad.Deactivate();
        contexto.Local.Deactivate();

        var outcome = await contexto.Service.UpdateAsync(
            contexto.Ficha.Id,
            new ProfesionalEdicionCommand(
                "COP 5",
                [contexto.Especialidad.Id],
                [contexto.Local.Id]),
            Token);

        outcome.Status.ShouldBe(ProfesionalEdicionStatus.Saved);
        contexto.Ficha.IsActive.ShouldBeFalse();
        contexto.Ficha.Colegiatura.ShouldBe("COP 5");
        contexto.Ficha.EspecialidadIds.ShouldBe([contexto.Especialidad.Id]);
        contexto.Ficha.LocalIds.ShouldBe([contexto.Local.Id]);
    }

    [Theory]
    [InlineData(ResultadoGuardadoProfesional.ColegiaturaDuplicada, ProfesionalEdicionStatus.DuplicateColegiatura)]
    [InlineData(ResultadoGuardadoProfesional.EspecialidadInactiva, ProfesionalEdicionStatus.EspecialidadInactiva)]
    [InlineData(ResultadoGuardadoProfesional.LocalInactivo, ProfesionalEdicionStatus.LocalInactivo)]
    [InlineData(ResultadoGuardadoProfesional.ActivacionIncompleta, ProfesionalEdicionStatus.ActivacionIncompleta)]
    public async Task A_write_rejection_of_the_edit_is_reported_as_its_status(
        ResultadoGuardadoProfesional writeResult,
        ProfesionalEdicionStatus expected)
    {
        // El catálogo estaba activo al leer: la regla se descubre al escribir, no antes.
        var contexto = Contexto.Crear(writeResult);

        var outcome = await contexto.Service.UpdateAsync(
            contexto.Ficha.Id,
            new ProfesionalEdicionCommand(
                null,
                [contexto.Especialidad.Id],
                [contexto.Local.Id]),
            Token);

        outcome.Status.ShouldBe(expected);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deactivating_keeps_the_assignments_and_the_account()
    {
        var contexto = Contexto.Crear();

        var outcome = await contexto.Service.SetStatusAsync(contexto.Ficha.Id, isActive: false, Token);

        outcome.Status.ShouldBe(ProfesionalEstadoStatus.Saved);
        contexto.Ficha.IsActive.ShouldBeFalse();
        contexto.Ficha.EspecialidadIds.ShouldBe([contexto.Especialidad.Id]);
        contexto.Ficha.LocalIds.ShouldBe([contexto.Local.Id]);
        contexto.Fichas.Saved.ShouldHaveSingleItem();
        contexto.Accounts.Updated.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reactivating_succeeds_when_the_ficha_keeps_an_active_specialty_and_local()
    {
        var contexto = Contexto.Crear();
        contexto.Ficha.Desactivar();

        var outcome = await contexto.Service.SetStatusAsync(contexto.Ficha.Id, isActive: true, Token);

        outcome.Status.ShouldBe(ProfesionalEstadoStatus.Saved);
        contexto.Ficha.IsActive.ShouldBeTrue();
        contexto.Fichas.Saved.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("especialidad")]
    [InlineData("local")]
    public async Task Reactivating_is_rejected_without_an_active_specialty_or_local(string inactivo)
    {
        var contexto = Contexto.Crear();
        contexto.Ficha.Desactivar();

        if (inactivo == "especialidad")
        {
            contexto.Especialidad.Deactivate();
        }
        else
        {
            contexto.Local.Deactivate();
        }

        var outcome = await contexto.Service.SetStatusAsync(contexto.Ficha.Id, isActive: true, Token);

        outcome.Status.ShouldBe(ProfesionalEstadoStatus.ActivacionIncompleta);
        contexto.Ficha.IsActive.ShouldBeFalse();
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_status_of_an_unknown_ficha_is_rejected()
    {
        var contexto = Contexto.Crear();

        var outcome = await contexto.Service.SetStatusAsync(Guid.NewGuid(), isActive: true, Token);

        outcome.Status.ShouldBe(ProfesionalEstadoStatus.NotFound);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_write_rejection_of_the_reactivation_is_reported_as_incomplete()
    {
        // La especialidad se desactivó entre la lectura y la escritura: el trigger lo rechaza.
        var contexto = Contexto.Crear(ResultadoGuardadoProfesional.ActivacionIncompleta);
        contexto.Ficha.Desactivar();

        var outcome = await contexto.Service.SetStatusAsync(contexto.Ficha.Id, isActive: true, Token);

        outcome.Status.ShouldBe(ProfesionalEstadoStatus.ActivacionIncompleta);
        contexto.Fichas.Saved.ShouldBeEmpty();
    }

    /// <summary>Ficha activa con una especialidad y un local, y dos alternativas activas.</summary>
    private sealed record Contexto(
        ProfesionalManagementService Service,
        Profesional Ficha,
        FakeProfesionales Fichas,
        FakeStaffAccounts Accounts,
        Especialidad Especialidad,
        Especialidad OtraEspecialidad,
        Local Local,
        Local OtroLocal)
    {
        public static Contexto Crear(ResultadoGuardadoProfesional saveResult = ResultadoGuardadoProfesional.Guardado)
        {
            var especialidad = ProfesionalSamples.NuevaEspecialidad("Odontología");
            var otra = ProfesionalSamples.NuevaEspecialidad("Ortodoncia");
            var local = StaffSamples.NewLocal("Sede Centro");
            var otro = StaffSamples.NewLocal("Sede Norte");
            var account = StaffSamples.Account(StaffRole.Professional, "profesional@mousedent.test");
            var ficha = ProfesionalSamples.Ficha(account.Id, especialidad.Id, local.Id);
            var accounts = new FakeStaffAccounts(account);
            var fichas = new FakeProfesionales(ficha) { SaveResult = saveResult };
            var service = ProfesionalSamples.Service(
                new FakeAltaStore(),
                accounts,
                new FakeCatalogo(especialidad, otra),
                new FakeLocalStore(local, otro),
                fichas);

            return new Contexto(service, ficha, fichas, accounts, especialidad, otra, local, otro);
        }
    }
}
