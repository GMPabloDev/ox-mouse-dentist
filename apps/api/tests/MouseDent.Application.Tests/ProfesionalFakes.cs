using MouseDent.Application.Catalogo;
using MouseDent.Application.Profesionales;
using MouseDent.Domain.Auth;
using MouseDent.Domain.Catalogo;
using MouseDent.Domain.Profesionales;

namespace MouseDent.Application.Tests;

/// <summary>
/// Escritura compuesta en memoria. Registra lo que el caso de uso pidió guardar y
/// devuelve el resultado configurado, de modo que las pruebas comprueban tanto el
/// alta aceptada como cada rechazo sin datos parciales.
/// </summary>
internal sealed class FakeAltaStore : IAltasDeProfesionales
{
    public List<(StaffAccount Account, Profesional Ficha)> Created { get; } = [];

    public List<Profesional> Linked { get; } = [];

    public ResultadoEscrituraAlta Result { get; init; } = ResultadoEscrituraAlta.Guardado;

    public Task<ResultadoEscrituraAlta> CrearConCuentaAsync(
        StaffAccount cuenta,
        Profesional ficha,
        CancellationToken cancellationToken)
    {
        if (Result is ResultadoEscrituraAlta.Guardado)
        {
            Created.Add((cuenta, ficha));
        }

        return Task.FromResult(Result);
    }

    public Task<ResultadoEscrituraAlta> VincularCuentaAsync(
        Profesional ficha,
        CancellationToken cancellationToken)
    {
        if (Result is ResultadoEscrituraAlta.Guardado)
        {
            Linked.Add(ficha);
        }

        return Task.FromResult(Result);
    }
}

/// <summary>Almacén de fichas en memoria para las lecturas del caso de uso.</summary>
internal sealed class FakeProfesionales(params Profesional[] stored) : IProfesionales
{
    private readonly List<Profesional> _fichas = [.. stored];

    public Task<Profesional?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_fichas.FirstOrDefault(ficha => ficha.Id == id));

    public Task<Profesional?> BuscarPorCuentaAsync(Guid staffAccountId, CancellationToken cancellationToken) =>
        Task.FromResult(_fichas.FirstOrDefault(ficha => ficha.StaffAccountId == staffAccountId));

    public Task<bool> ColegiaturaEstaTomadaAsync(
        string colegiaturaNormalizada,
        Guid? excluirProfesionalId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_fichas.Any(ficha =>
            ficha.ColegiaturaNormalizada == colegiaturaNormalizada && ficha.Id != excluirProfesionalId));

    public Task<ResultadoGuardadoProfesional> GuardarAsync(
        Profesional profesional,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>Catálogo en memoria. Las pruebas del alta solo leen las especialidades.</summary>
internal sealed class FakeCatalogo(params Especialidad[] stored) : ICatalogo
{
    private readonly List<Especialidad> _especialidades = [.. stored];

    public Task<IReadOnlyList<Especialidad>> ListEspecialidadesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Especialidad>>(_especialidades);

    public Task<IReadOnlyList<Servicio>> ListServiciosAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Especialidad?> FindEspecialidadAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Servicio?> FindServicioAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> HasActiveServicesAsync(Guid especialidadId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CatalogoSaveResult> SaveAsync(Especialidad especialidad, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CatalogoSaveResult> SaveAsync(Servicio servicio, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>Listado en memoria. Registra el filtro recibido y devuelve las fichas configuradas.</summary>
internal sealed class FakeListadoDeProfesionales(params ProfesionalResumen[] stored) : IListadoDeProfesionales
{
    public List<ProfesionalFiltro> Received { get; } = [];

    public Task<IReadOnlyList<ProfesionalResumen>> ListAsync(
        ProfesionalFiltro filtro,
        CancellationToken cancellationToken)
    {
        Received.Add(filtro);

        return Task.FromResult<IReadOnlyList<ProfesionalResumen>>(stored);
    }
}

/// <summary>Especialidades y fichas con las que se construyen los casos de prueba del alta.</summary>
internal static class ProfesionalSamples
{
    public static Especialidad NuevaEspecialidad(string name, bool isActive = true)
    {
        var especialidad = Especialidad.Create(name, null);
        if (!isActive)
        {
            especialidad.Deactivate();
        }

        return especialidad;
    }

    public static Profesional Ficha(
        Guid staffAccountId,
        Guid especialidadId,
        Guid localId,
        string? colegiatura = null) =>
        Profesional.Crear(
            staffAccountId,
            colegiatura,
            [especialidadId],
            [localId],
            new CatalogoActivo([especialidadId], [localId]));

    public static ProfesionalAltaCommand Command(
        string email,
        IEnumerable<Guid> especialidadIds,
        IEnumerable<Guid> localIds,
        string? colegiatura = null,
        string? displayName = null) =>
        new(
            displayName ?? "Persona de alta",
            email,
            colegiatura,
            [.. especialidadIds],
            [.. localIds]);

    public static ProfesionalManagementService Service(
        FakeAltaStore altas,
        FakeStaffAccounts accounts,
        FakeCatalogo catalogo,
        FakeLocalStore locales,
        FakeProfesionales? profesionales = null,
        FakeEmailSender? email = null,
        FakeListadoDeProfesionales? listado = null) =>
        new(
            altas,
            profesionales ?? new FakeProfesionales(),
            listado ?? new FakeListadoDeProfesionales(),
            accounts,
            catalogo,
            locales,
            StaffSamples.AccessService(
                accounts,
                new FakeAccessLinks(),
                email ?? new FakeEmailSender()));
}
