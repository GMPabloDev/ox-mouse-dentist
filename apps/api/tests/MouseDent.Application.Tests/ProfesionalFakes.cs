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

/// <summary>Almacén de fichas en memoria para las lecturas y las escrituras del caso de uso.</summary>
internal sealed class FakeProfesionales(params Profesional[] stored) : IProfesionales
{
    private readonly List<Profesional> _fichas = [.. stored];

    /// <summary>Resultado que devuelve la escritura; solo <c>Guardado</c> conserva la ficha.</summary>
    public ResultadoGuardadoProfesional SaveResult { get; init; } = ResultadoGuardadoProfesional.Guardado;

    /// <summary>
    /// Cuando es verdadero, el almacén guarda copias y reconstruye la ficha con
    /// <see cref="Profesional.Restaurar"/> en cada lectura, como la persistencia real.
    /// Así la prueba detecta a un consumidor que cachea la ficha: una copia guardada por
    /// él no ve los cambios posteriores del almacén.
    /// </summary>
    public bool ReconstruirAlLeer { get; init; }

    /// <summary>Fichas que la escritura aceptó, en orden.</summary>
    public List<Profesional> Saved { get; } = [];

    public Task<Profesional?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_fichas.Where(ficha => ficha.Id == id).Select(Leer).FirstOrDefault());

    public Task<Profesional?> BuscarPorCuentaAsync(Guid staffAccountId, CancellationToken cancellationToken) =>
        Task.FromResult(_fichas.FirstOrDefault(ficha => ficha.StaffAccountId == staffAccountId) is { } ficha
            ? Leer(ficha)
            : null);

    public Task<ResultadoGuardadoProfesional> GuardarAsync(
        Profesional profesional,
        CancellationToken cancellationToken)
    {
        if (SaveResult is not ResultadoGuardadoProfesional.Guardado)
        {
            return Task.FromResult(SaveResult);
        }

        var guardada = Leer(profesional);
        var index = _fichas.FindIndex(ficha => ficha.Id == profesional.Id);
        if (index < 0)
        {
            _fichas.Add(guardada);
        }
        else
        {
            _fichas[index] = guardada;
        }

        Saved.Add(profesional);

        return Task.FromResult(SaveResult);
    }

    /// <summary>
    /// Ficha que entrega una lectura. Con <see cref="ReconstruirAlLeer"/> es una copia
    /// nueva, de modo que nadie comparte la instancia con el almacén.
    /// </summary>
    private Profesional Leer(Profesional ficha) =>
        ReconstruirAlLeer
            ? Profesional.Restaurar(
                ficha.Id,
                ficha.StaffAccountId,
                ficha.Colegiatura,
                ficha.IsActive,
                ficha.EspecialidadIds,
                ficha.LocalIds)
            : ficha;
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
    private readonly ProfesionalResumen[] _resumenes = stored;

    public List<ProfesionalFiltro> Received { get; } = [];

    public Task<IReadOnlyList<ProfesionalResumen>> ListAsync(
        ProfesionalFiltro filtro,
        CancellationToken cancellationToken)
    {
        Received.Add(filtro);

        return Task.FromResult<IReadOnlyList<ProfesionalResumen>>(_resumenes);
    }

    public Task<ProfesionalResumen?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_resumenes.FirstOrDefault(resumen => resumen.Id == id));
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
