using MouseDent.Application.Catalogo;
using MouseDent.Domain.Catalogo;
using Shouldly;

namespace MouseDent.Application.Tests;

public sealed class CatalogoServiceTests
{
    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reception_reads_only_active_records_and_admin_can_filter_by_state()
    {
        var dental = Especialidad.Create("Dental", null);
        var orthodontics = Especialidad.Create("Ortodoncia", null);
        orthodontics.Deactivate();
        var cleaning = Servicio.Create("Limpieza", null, dental);
        var whitening = Servicio.Create("Blanqueamiento", null, dental);
        whitening.Deactivate();
        var service = new CatalogoService(new FakeCatalogo([dental, orthodontics], [cleaning, whitening]));

        (await service.EspecialidadesAsync(null, null, false, Token)).ShouldBe([dental]);
        (await service.ServiciosAsync(null, null, null, false, Token)).ShouldBe([cleaning]);

        // El filtro de estado es de administración: a recepción no le quita lo activo.
        (await service.EspecialidadesAsync(null, false, false, Token)).ShouldBe([dental]);
        (await service.ServiciosAsync(null, false, null, false, Token)).ShouldBe([cleaning]);

        (await service.EspecialidadesAsync(null, null, true, Token)).Count.ShouldBe(2);
        (await service.EspecialidadesAsync(null, false, true, Token)).ShouldBe([orthodontics]);
        (await service.ServiciosAsync(null, true, null, true, Token)).ShouldBe([cleaning]);
        (await service.ServiciosAsync(null, false, null, true, Token)).ShouldBe([whitening]);
    }

    [Fact]
    public async Task Search_ignores_outer_spaces_and_case_and_services_filter_by_specialty()
    {
        var dental = Especialidad.Create("Dental", null);
        var orthodontics = Especialidad.Create("Ortodoncia", null);
        var cleaning = Servicio.Create("Limpieza dental", null, dental);
        var braces = Servicio.Create("Brackets", null, orthodontics);
        var service = new CatalogoService(new FakeCatalogo([dental, orthodontics], [cleaning, braces]));

        (await service.EspecialidadesAsync("  DENTAL  ", null, true, Token)).ShouldBe([dental]);
        (await service.EspecialidadesAsync("  ", null, true, Token)).Count.ShouldBe(2);
        (await service.ServiciosAsync("  dental ", null, null, true, Token)).ShouldBe([cleaning]);
        (await service.ServiciosAsync(null, null, orthodontics.Id, true, Token)).ShouldBe([braces]);
        (await service.ServiciosAsync("Limpieza", null, orthodontics.Id, true, Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_a_service_under_an_inactive_specialty_is_rejected()
    {
        var orthodontics = Especialidad.Create("Ortodoncia", null);
        orthodontics.Deactivate();
        var catalogo = new FakeCatalogo([orthodontics], []);
        var service = new CatalogoService(catalogo);

        var outcome = await service.SaveServicioAsync(null, "Brackets", null, orthodontics.Id, Token);

        outcome.Status.ShouldBe(CatalogoOutcomeStatus.InactiveDestination);
        outcome.Entity.ShouldBeNull();
        catalogo.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Moving_a_service_to_an_inactive_specialty_is_rejected_and_keeps_the_destination()
    {
        var dental = Especialidad.Create("Dental", null);
        var orthodontics = Especialidad.Create("Ortodoncia", null);
        orthodontics.Deactivate();
        var cleaning = Servicio.Create("Limpieza", null, dental);
        var catalogo = new FakeCatalogo([dental, orthodontics], [cleaning]);
        var service = new CatalogoService(catalogo);

        var outcome = await service.SaveServicioAsync(cleaning.Id, "Limpieza", null, orthodontics.Id, Token);

        outcome.Status.ShouldBe(CatalogoOutcomeStatus.InactiveDestination);
        cleaning.EspecialidadId.ShouldBe(dental.Id);
        catalogo.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Moving_a_service_to_another_active_specialty_updates_it()
    {
        var dental = Especialidad.Create("Dental", null);
        var orthodontics = Especialidad.Create("Ortodoncia", null);
        var cleaning = Servicio.Create("Limpieza", null, dental);
        var catalogo = new FakeCatalogo([dental, orthodontics], [cleaning]);
        var service = new CatalogoService(catalogo);

        var outcome = await service.SaveServicioAsync(
            cleaning.Id,
            "  Limpieza dental  ",
            "  Cada seis meses  ",
            orthodontics.Id,
            Token);

        outcome.Status.ShouldBe(CatalogoOutcomeStatus.Saved);
        outcome.Entity.ShouldBe(cleaning);
        cleaning.EspecialidadId.ShouldBe(orthodontics.Id);
        cleaning.Name.ShouldBe("Limpieza dental");
        cleaning.Description.ShouldBe("Cada seis meses");
        catalogo.Saved.ShouldContain(cleaning);
    }

    [Fact]
    public async Task Unknown_records_and_destinations_are_reported_as_not_found()
    {
        var dental = Especialidad.Create("Dental", null);
        var service = new CatalogoService(new FakeCatalogo([dental], []));
        var missing = Guid.NewGuid();

        (await service.SaveEspecialidadAsync(missing, "Ortodoncia", null, Token)).Status
            .ShouldBe(CatalogoOutcomeStatus.NotFound);
        (await service.SetEspecialidadAsync(missing, true, Token)).Status
            .ShouldBe(CatalogoOutcomeStatus.NotFound);
        (await service.SaveServicioAsync(missing, "Limpieza", null, dental.Id, Token)).Status
            .ShouldBe(CatalogoOutcomeStatus.NotFound);
        (await service.SaveServicioAsync(null, "Limpieza", null, missing, Token)).Status
            .ShouldBe(CatalogoOutcomeStatus.NotFound);
        (await service.SetServicioAsync(missing, true, Token)).Status
            .ShouldBe(CatalogoOutcomeStatus.NotFound);
    }

    [Fact]
    public async Task A_specialty_with_active_services_cannot_be_deactivated()
    {
        var dental = Especialidad.Create("Dental", null);
        var cleaning = Servicio.Create("Limpieza", null, dental);
        var catalogo = new FakeCatalogo([dental], [cleaning]);
        var service = new CatalogoService(catalogo);

        var outcome = await service.SetEspecialidadAsync(dental.Id, false, Token);

        outcome.Status.ShouldBe(CatalogoOutcomeStatus.ActiveServices);
        dental.IsActive.ShouldBeTrue();
        catalogo.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_specialty_without_active_services_is_deactivated_and_can_be_reactivated()
    {
        var dental = Especialidad.Create("Dental", null);
        var cleaning = Servicio.Create("Limpieza", null, dental);
        cleaning.Deactivate();
        var catalogo = new FakeCatalogo([dental], [cleaning]);
        var service = new CatalogoService(catalogo);

        var deactivated = await service.SetEspecialidadAsync(dental.Id, false, Token);

        deactivated.Status.ShouldBe(CatalogoOutcomeStatus.Saved);
        dental.IsActive.ShouldBeFalse();

        var reactivated = await service.SetEspecialidadAsync(dental.Id, true, Token);

        reactivated.Status.ShouldBe(CatalogoOutcomeStatus.Saved);
        dental.IsActive.ShouldBeTrue();
        cleaning.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Reactivating_a_service_requires_its_specialty_to_be_active()
    {
        var dental = Especialidad.Create("Dental", null);
        var cleaning = Servicio.Create("Limpieza", null, dental);
        cleaning.Deactivate();
        dental.Deactivate();
        var catalogo = new FakeCatalogo([dental], [cleaning]);
        var service = new CatalogoService(catalogo);

        var outcome = await service.SetServicioAsync(cleaning.Id, true, Token);

        outcome.Status.ShouldBe(CatalogoOutcomeStatus.InactiveDestination);
        cleaning.IsActive.ShouldBeFalse();
        catalogo.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_name_taken_in_the_store_is_reported_as_a_duplicate()
    {
        var dental = Especialidad.Create("Dental", null);
        var catalogo = new FakeCatalogo([dental], [], CatalogoSaveResult.DuplicateName);
        var service = new CatalogoService(catalogo);

        var specialty = await service.SaveEspecialidadAsync(null, "Dental", null, Token);
        var created = await service.SaveServicioAsync(null, "Limpieza", null, dental.Id, Token);

        specialty.Status.ShouldBe(CatalogoOutcomeStatus.DuplicateName);
        specialty.Entity.ShouldBeNull();
        created.Status.ShouldBe(CatalogoOutcomeStatus.DuplicateName);
    }

    private sealed class FakeCatalogo(
        Especialidad[] especialidades,
        Servicio[] servicios,
        CatalogoSaveResult saveResult = CatalogoSaveResult.Saved) : ICatalogo
    {
        private readonly List<Especialidad> _especialidades = [.. especialidades];
        private readonly List<Servicio> _servicios = [.. servicios];

        public List<object> Saved { get; } = [];

        public Task<IReadOnlyList<Especialidad>> ListEspecialidadesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Especialidad>>(_especialidades);

        public Task<IReadOnlyList<Servicio>> ListServiciosAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Servicio>>(_servicios);

        public Task<Especialidad?> FindEspecialidadAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_especialidades.FirstOrDefault(x => x.Id == id));

        public Task<Servicio?> FindServicioAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_servicios.FirstOrDefault(x => x.Id == id));

        public Task<bool> HasActiveServicesAsync(Guid especialidadId, CancellationToken cancellationToken) =>
            Task.FromResult(_servicios.Any(x => x.EspecialidadId == especialidadId && x.IsActive));

        public Task<CatalogoSaveResult> SaveAsync(
            Especialidad especialidad,
            CancellationToken cancellationToken)
        {
            if (saveResult == CatalogoSaveResult.Saved && !_especialidades.Contains(especialidad))
            {
                _especialidades.Add(especialidad);
            }

            Saved.Add(especialidad);
            return Task.FromResult(saveResult);
        }

        public Task<CatalogoSaveResult> SaveAsync(Servicio servicio, CancellationToken cancellationToken)
        {
            if (saveResult == CatalogoSaveResult.Saved && !_servicios.Contains(servicio))
            {
                _servicios.Add(servicio);
            }

            Saved.Add(servicio);
            return Task.FromResult(saveResult);
        }
    }
}
