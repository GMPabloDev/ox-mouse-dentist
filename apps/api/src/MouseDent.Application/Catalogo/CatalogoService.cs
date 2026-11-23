using MouseDent.Domain.Catalogo;

namespace MouseDent.Application.Catalogo;

public sealed class CatalogoService(ICatalogo catalogo)
{
    public async Task<IReadOnlyList<Especialidad>> EspecialidadesAsync(
        string? search,
        bool? active,
        bool admin,
        CancellationToken cancellationToken)
    {
        var stored = await catalogo.ListEspecialidadesAsync(cancellationToken);

        return [.. stored.Where(x => Visible(x.IsActive, active, admin) && Matches(x.Name, search))];
    }

    public async Task<IReadOnlyList<Servicio>> ServiciosAsync(
        string? search,
        bool? active,
        Guid? especialidadId,
        bool admin,
        CancellationToken cancellationToken)
    {
        var stored = await catalogo.ListServiciosAsync(cancellationToken);

        return
        [
            .. stored.Where(x =>
                Visible(x.IsActive, active, admin)
                && (especialidadId is null || x.EspecialidadId == especialidadId)
                && Matches(x.Name, search)),
        ];
    }

    public async Task<CatalogoOutcome<Especialidad>> SaveEspecialidadAsync(
        Guid? id,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        var entity = id is null
            ? Especialidad.Create(name, description)
            : await catalogo.FindEspecialidadAsync(id.Value, cancellationToken);
        if (entity is null)
        {
            return CatalogoOutcome<Especialidad>.NotFound();
        }

        if (id is not null)
        {
            entity.Update(name, description);
        }

        return Report(entity, await catalogo.SaveAsync(entity, cancellationToken));
    }

    public async Task<CatalogoOutcome<Especialidad>> SetEspecialidadAsync(
        Guid id,
        bool active,
        CancellationToken cancellationToken)
    {
        var entity = await catalogo.FindEspecialidadAsync(id, cancellationToken);
        if (entity is null)
        {
            return CatalogoOutcome<Especialidad>.NotFound();
        }

        if (active)
        {
            entity.Activate();
        }
        else
        {
            if (await catalogo.HasActiveServicesAsync(id, cancellationToken))
            {
                return CatalogoOutcome<Especialidad>.ActiveServices();
            }

            entity.Deactivate();
        }

        return Report(entity, await catalogo.SaveAsync(entity, cancellationToken));
    }

    public async Task<CatalogoOutcome<Servicio>> SaveServicioAsync(
        Guid? id,
        string name,
        string? description,
        Guid especialidadId,
        CancellationToken cancellationToken)
    {
        var entity = id is null ? null : await catalogo.FindServicioAsync(id.Value, cancellationToken);
        if (id is not null && entity is null)
        {
            return CatalogoOutcome<Servicio>.NotFound();
        }

        var destination = await catalogo.FindEspecialidadAsync(especialidadId, cancellationToken);
        if (destination is null)
        {
            return CatalogoOutcome<Servicio>.NotFound();
        }

        if ((entity is null || entity.EspecialidadId != especialidadId) && !destination.IsActive)
        {
            return CatalogoOutcome<Servicio>.InactiveDestination();
        }

        if (entity is null)
        {
            entity = Servicio.Create(name, description, destination);
        }
        else
        {
            if (entity.EspecialidadId != especialidadId)
            {
                entity.MoveTo(destination);
            }

            entity.Update(name, description);
        }

        return Report(entity, await catalogo.SaveAsync(entity, cancellationToken));
    }

    public async Task<CatalogoOutcome<Servicio>> SetServicioAsync(
        Guid id,
        bool active,
        CancellationToken cancellationToken)
    {
        var entity = await catalogo.FindServicioAsync(id, cancellationToken);
        if (entity is null)
        {
            return CatalogoOutcome<Servicio>.NotFound();
        }

        if (active)
        {
            var destination = await catalogo.FindEspecialidadAsync(entity.EspecialidadId, cancellationToken);
            if (destination is null || !destination.IsActive)
            {
                return CatalogoOutcome<Servicio>.InactiveDestination();
            }

            entity.Activate(destination);
        }
        else
        {
            entity.Deactivate();
        }

        return Report(entity, await catalogo.SaveAsync(entity, cancellationToken));
    }

    /// <summary>Recepción y profesionales solo ven lo activo; el filtro de estado es de administración.</summary>
    private static bool Visible(bool isActive, bool? active, bool admin) =>
        admin ? active is null || isActive == active : isActive;

    private static bool Matches(string name, string? search) =>
        string.IsNullOrWhiteSpace(search) || name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    private static CatalogoOutcome<T> Report<T>(T entity, CatalogoSaveResult result)
        where T : class =>
        result switch
        {
            CatalogoSaveResult.Saved => CatalogoOutcome<T>.Saved(entity),
            CatalogoSaveResult.DuplicateName => CatalogoOutcome<T>.DuplicateName(),
            CatalogoSaveResult.EspecialidadHasActiveServices => CatalogoOutcome<T>.ActiveServices(),
            CatalogoSaveResult.DestinationInactive => CatalogoOutcome<T>.InactiveDestination(),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Resultado de guardado sin traducción."),
        };
}
